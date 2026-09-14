-- Design 4.3. server_id leads the primary key, as it leads every
-- server-scoped key in 0001 - solo_execution 4: no globally meaningful IDs,
-- because a merge must be a re-keying exercise.
CREATE TABLE IF NOT EXISTS wave_issuances (
  server_id    integer     NOT NULL,
  issuance_id  uuid        NOT NULL,
  player_id    uuid        NOT NULL,
  wave_id      integer     NOT NULL,
  seed         bigint      NOT NULL,
  issued_at    timestamptz NOT NULL DEFAULT now(),
  expires_at   timestamptz NOT NULL,
  consumed_at  timestamptz,
  PRIMARY KEY (server_id, issuance_id),
  -- Composite, so a handler scoped to one server cannot bind an issuance to
  -- another server's player. 0001 added these to wallets, ledger and
  -- campaign_progress for the same reason, before money landed on them.
  FOREIGN KEY (server_id, player_id) REFERENCES players (server_id, player_id)
);

-- ONE LIVE ISSUANCE PER PLAYER, in Postgres rather than in a handler.
-- design 2.1's seed-shop defence, and the load-bearing half of wave/start.
CREATE UNIQUE INDEX IF NOT EXISTS wave_issuances_one_live
  ON wave_issuances (server_id, player_id)
  WHERE consumed_at IS NULL;

-- The replay-cap count reads this - design 4.1 check 2 - so it must be
-- indexed by what that query filters on.
CREATE INDEX IF NOT EXISTS wave_issuances_replay_count
  ON wave_issuances (server_id, player_id, wave_id, issued_at)
  WHERE consumed_at IS NOT NULL;

-- consumed_at is WRITE-ONCE. The issuance is the ledger's guard against a
-- second payout; a consumed_at that can be set back to NULL is not a guard.
CREATE OR REPLACE FUNCTION reject_consumed_at_rewrite() RETURNS trigger AS $$
BEGIN
  IF OLD.consumed_at IS NOT NULL AND NEW.consumed_at IS DISTINCT FROM OLD.consumed_at THEN
    RAISE EXCEPTION 'wave_issuances.consumed_at is write-once (attempted % -> %)',
      OLD.consumed_at, NEW.consumed_at;
  END IF;
  RETURN NEW;
END;
$$ LANGUAGE plpgsql;

-- DROP first. 0002's CREATE TRIGGER accounts_server_id_immutable is the one
-- statement in that file which is not re-runnable, and this is the same
-- pattern - recorded in the Phase 4 followups as worth fixing, and worth not
-- repeating.
DROP TRIGGER IF EXISTS wave_issuances_consumed_at_write_once ON wave_issuances;
CREATE TRIGGER wave_issuances_consumed_at_write_once
  BEFORE UPDATE ON wave_issuances
  FOR EACH ROW
  EXECUTE FUNCTION reject_consumed_at_rewrite();

GRANT SELECT, INSERT, UPDATE ON wave_issuances TO broodline_app;
-- No DELETE. The sweep at Task 12 runs as the owner; a handler has no reason
-- to delete an issuance and every reason not to be able to.

ALTER TABLE wave_issuances ENABLE ROW LEVEL SECURITY;
ALTER TABLE wave_issuances FORCE ROW LEVEL SECURITY;
CREATE POLICY server_isolation ON wave_issuances
  FOR ALL
  USING      (server_id = NULLIF(current_setting('app.server_id', true), '')::int)
  WITH CHECK (server_id = NULLIF(current_setting('app.server_id', true), '')::int);
