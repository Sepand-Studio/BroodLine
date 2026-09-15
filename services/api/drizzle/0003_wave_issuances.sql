-- Design 4.3. server_id leads the primary key, as it leads every
-- server-scoped key in 0001 - solo_execution 4: no globally meaningful IDs,
-- because a merge must be a re-keying exercise.
--
-- Amended after review (C1): the first version of this migration used a
-- single `consumed_at` column and keyed the one-live index on
-- `consumed_at IS NULL`. That locked a player out: an abandoned wave leaves
-- a row that is expired but never consumed, so it still satisfies the
-- index's predicate. `wave/start` treats it as expired and tries to insert a
-- fresh row, which collides on the index - 23505, for at least an hour,
-- every time anyone backgrounds the app mid-wave. Postgres also forbids the
-- fix "just add expiry to the predicate": index predicates must be
-- IMMUTABLE and now() is STABLE, so `expires_at > now()` is rejected
-- outright, not merely unwise.
--
-- The fix is one terminal state, reached only by a write, so the index
-- predicate and the handler's liveness test are the same expression and can
-- never disagree:
--   settled_at  - NULL while live, set once to the moment it stopped being live
--   settlement  - NULL iff settled_at is NULL, else 'consumed' or 'expired'
-- `wave/start` settles a stale-but-live row 'expired' and inserts the new
-- row in the SAME transaction; nothing waits on a cron to be allowed to
-- play.
CREATE TABLE IF NOT EXISTS wave_issuances (
  server_id    integer     NOT NULL,
  issuance_id  uuid        NOT NULL,
  player_id    uuid        NOT NULL,
  wave_id      integer     NOT NULL,
  -- bigint is int8 (signed, -2^63..2^63-1); the engine's seed is a ulong
  -- (0..2^64-1). A seed at or above 2^63 would error on insert, or - if
  -- someone "fixed" that with a cast - sign-flip into a DIFFERENT wave,
  -- presenting as a hash mismatch on an honest submission. The generator is
  -- constrained to [0, 2^63-1] (Task 5); this CHECK makes that contract loud
  -- at the one place every seed passes through, rather than implicit at
  -- four call sites.
  seed         bigint      NOT NULL CHECK (seed >= 0),
  issued_at    timestamptz NOT NULL DEFAULT now(),
  expires_at   timestamptz NOT NULL,
  settled_at   timestamptz,
  settlement   text CHECK (settlement IN ('consumed', 'expired')),
  -- settlement is NULL exactly when settled_at is NULL - a row is either
  -- live with neither set, or settled with both set together.
  CHECK ((settled_at IS NULL) = (settlement IS NULL)),
  PRIMARY KEY (server_id, issuance_id),
  -- Composite, so a handler scoped to one server cannot bind an issuance to
  -- another server's player. 0001 added these to wallets, ledger and
  -- campaign_progress for the same reason, before money landed on them.
  FOREIGN KEY (server_id, player_id) REFERENCES players (server_id, player_id)
);

-- ONE LIVE ISSUANCE PER PLAYER, in Postgres rather than in a handler.
-- design 2.1's seed-shop defence, and the load-bearing half of wave/start.
-- Keyed on settled_at, NOT on expiry: see the note above C1 - expiry cannot
-- appear in a partial index's predicate, and "live" now means exactly one
-- thing everywhere it is checked.
CREATE UNIQUE INDEX IF NOT EXISTS wave_issuances_one_live
  ON wave_issuances (server_id, player_id)
  WHERE settled_at IS NULL;

-- The replay-cap count reads this - design 4.1 check 2 - so it must be
-- indexed by what that query filters on. Only 'consumed' rows count:
-- an 'expired' row was never played, and counting it would let an abandoned
-- wave silently burn one of the player's three daily replays.
CREATE INDEX IF NOT EXISTS wave_issuances_replay_count
  ON wave_issuances (server_id, player_id, wave_id, issued_at)
  WHERE settlement = 'consumed';

-- settlement is WRITE-ONCE. The issuance is the ledger's guard against a
-- second payout; a settlement that can be rewritten - reverted to NULL, or
-- silently moved from 'consumed' to 'expired' or vice versa - is not a
-- guard at all.
CREATE OR REPLACE FUNCTION reject_wave_issuance_settlement_rewrite() RETURNS trigger AS $$
BEGIN
  IF OLD.settled_at IS NOT NULL
     AND (NEW.settled_at IS DISTINCT FROM OLD.settled_at
          OR NEW.settlement IS DISTINCT FROM OLD.settlement) THEN
    RAISE EXCEPTION 'wave_issuances.settlement is write-once (attempted % % -> % %)',
      OLD.settled_at, OLD.settlement, NEW.settled_at, NEW.settlement;
  END IF;
  RETURN NEW;
END;
$$ LANGUAGE plpgsql;

-- DROP first. 0002's CREATE TRIGGER accounts_server_id_immutable is the one
-- statement in that file which is not re-runnable, and this is the same
-- pattern - recorded in the Phase 4 followups as worth fixing, and worth not
-- repeating.
DROP TRIGGER IF EXISTS wave_issuances_settlement_write_once ON wave_issuances;
CREATE TRIGGER wave_issuances_settlement_write_once
  BEFORE UPDATE ON wave_issuances
  FOR EACH ROW
  EXECUTE FUNCTION reject_wave_issuance_settlement_rewrite();

GRANT SELECT, INSERT, UPDATE ON wave_issuances TO broodline_app;
-- No DELETE. The sweep at Task 12 runs as the owner; a handler has no reason
-- to delete an issuance and every reason not to be able to.

ALTER TABLE wave_issuances ENABLE ROW LEVEL SECURITY;
ALTER TABLE wave_issuances FORCE ROW LEVEL SECURITY;
-- DROP first (M1): CREATE POLICY has no IF NOT EXISTS, and this file is
-- otherwise re-runnable everywhere, including the DROP TRIGGER above - this
-- is 0002's one non-re-runnable statement, not repeated a second time.
DROP POLICY IF EXISTS server_isolation ON wave_issuances;
CREATE POLICY server_isolation ON wave_issuances
  FOR ALL
  USING      (server_id = NULLIF(current_setting('app.server_id', true), '')::int)
  WITH CHECK (server_id = NULLIF(current_setting('app.server_id', true), '')::int);
