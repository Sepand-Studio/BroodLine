-- Design 3, the whole of it that is storage-shaped: the roster, the map's
-- node state, and the splice record. Six tables, all SERVER-SCOPED -
-- server_id leads every primary key and every index, as it does in 0001 and
-- 0003, because solo_execution 4 makes a merge a re-keying exercise and
-- nothing globally meaningful may exist.
--
-- Three of the constraints below do work a handler would otherwise be
-- trusted to remember, and each is a constraint rather than a check in code
-- for the same reason 0003's one-live index is an index: a handler can be
-- forgotten and a constraint cannot.
--
-- WHAT THIS FILE DOES NOT FOLLOW FROM THE TASK BRIEF, and why:
--   * The RLS policy is 0002's shape verbatim - named `server_isolation`,
--     FOR ALL, USING *and* WITH CHECK, reading the setting through
--     NULLIF(current_setting('app.server_id', true), ''). The brief's
--     snippet had USING alone (so a handler scoped to server 1 could still
--     INSERT a row onto server 2 - isolation.test.ts pins that this is the
--     more damaging direction) and current_setting() without the missing_ok
--     argument (so an unscoped query RAISES instead of returning zero rows,
--     which is the opposite of 0002's default-deny).
--   * GRANTs to broodline_app. The brief omits them entirely; without them
--     every handler in Tasks 5-7 fails with "permission denied", because
--     0002's grant is hardcoded against the five tables that existed then.
--   * Re-runnable idioms throughout (IF NOT EXISTS, CREATE OR REPLACE, DROP
--     ... IF EXISTS before CREATE), which 0003 and 0004 adopted after 0002's
--     two non-re-runnable statements were recorded as worth not repeating.

CREATE TABLE IF NOT EXISTS creatures (
  server_id      integer NOT NULL,
  creature_id    uuid    NOT NULL DEFAULT gen_random_uuid(),
  player_id      uuid    NOT NULL,
  species        text    NOT NULL,
  generation     integer NOT NULL,
  -- A TraitInstance is (trait, coverage_tier) - data_model 2. Two combat
  -- slots: 1 is the locked slot, 2 is the rolled one (design 5.2).
  trait_1        text    NOT NULL,
  tier_1         integer,
  trait_2        text    NOT NULL,
  tier_2         integer,
  instinct       text    NOT NULL,
  name           text,
  is_founder     boolean NOT NULL DEFAULT false,
  parent_a       uuid,
  parent_b       uuid,
  hp_current     integer NOT NULL,
  regen_until    timestamptz,
  committed_to   uuid,
  acquired_at    timestamptz NOT NULL DEFAULT now(),

  PRIMARY KEY (server_id, creature_id),

  -- NULL is an Aberrant. Zero is nothing, and must never be storable:
  -- data_model 2 refuses to conflate them because zero would sort and
  -- display as "less than tier I", and design 5.3's recessive downtier
  -- floors at I, so nothing else ever produces a zero either.
  --
  -- Named for the DOMAIN's word, not the column's. A CHECK violation prints
  -- only the constraint name, so `tier_1_not_zero` would have handed a
  -- reader - and the test asserting on it - a message that never says what
  -- the value means.
  CONSTRAINT coverage_tier_1_not_zero CHECK (tier_1 IS NULL OR tier_1 BETWEEN 1 AND 3),
  CONSTRAINT coverage_tier_2_not_zero CHECK (tier_2 IS NULL OR tier_2 BETWEEN 1 AND 3),
  -- Base stock is 1, a child is max(parents) + 1 - design 3.1.
  CONSTRAINT generation_positive CHECK (generation >= 1),
  -- data_model 2: only Founders may be named.
  CONSTRAINT only_founders_named CHECK (name IS NULL OR is_founder),

  FOREIGN KEY (server_id, player_id) REFERENCES players (server_id, player_id),

  -- COMPOSITE, so a parent on another server is not merely wrong but
  -- UNREPRESENTABLE. A single-column FK would only prove the parent exists
  -- SOMEWHERE - 0001 makes the same argument for players.account_id, before
  -- money landed on it.
  --
  -- !! OPEN AGAINST TASK 7 (the prune), and recorded here because this is
  -- where the constraint lives. These FKs are NO ACTION, so a creature that
  -- is any creature's parent CANNOT BE DELETED. Every dead ancestor is
  -- pointed at by its own child, so under this FK design 3.2's "prune
  -- everything else to a tombstone" can delete nothing at all, and
  -- data_model 4's nine-thousand-rows-per-player arithmetic is not solved.
  -- The two resolutions both cost something the design has not chosen
  -- between - ON DELETE SET NULL (parent_a) orphans the tombstone the child
  -- was supposed to be able to render, and dropping the FK gives up the
  -- cross-server guarantee above - so neither is taken here unilaterally.
  FOREIGN KEY (server_id, parent_a)  REFERENCES creatures (server_id, creature_id),
  FOREIGN KEY (server_id, parent_b)  REFERENCES creatures (server_id, creature_id)
);

CREATE INDEX IF NOT EXISTS creatures_by_player ON creatures (server_id, player_id, acquired_at);
-- Partial: the roster screen and the Hatchery cap both ask only about
-- uncommitted creatures, and committed ones are the minority.
CREATE INDEX IF NOT EXISTS creatures_available ON creatures (server_id, player_id)
  WHERE committed_to IS NULL;

-- design 3.2 / data_model 4. About forty bytes a row, kept indefinitely:
-- enough to render "an unnamed Gen-4 Vetch" where a tree reaches past the
-- retained depth.
CREATE TABLE IF NOT EXISTS creature_tombstones (
  server_id   integer NOT NULL,
  creature_id uuid    NOT NULL,
  species     text    NOT NULL,
  generation  integer NOT NULL,
  was_founder boolean NOT NULL,
  PRIMARY KEY (server_id, creature_id)
);

-- Ids are never reused across the LIVE and PRUNED spaces. Postgres cannot
-- express a uniqueness constraint spanning two tables, so the guard is a
-- trigger - and it is a trigger rather than a handler check for the same
-- reason the one-live issuance is an index. A reused id attaches a dead
-- creature's lineage to a living one, which gets reported as a ghost rather
-- than as a bug.
--
-- The EXISTS below is read under the reader's own row security (plpgsql is
-- SECURITY INVOKER, as 0002's and 0003's trigger functions are). That is
-- correct for both roles that exist: broodline_app only ever inserts inside
-- withServer, where app.server_id is set and the tombstone policy admits
-- exactly the rows on that server; and the migration/owner connection is a
-- superuser, which is exempt from RLS entirely. A future NON-superuser
-- owner writing with no app.server_id would see an empty tombstone table
-- here and the guard would pass silently - noted because nothing else in
-- this repo has a trigger that READS a table.
CREATE OR REPLACE FUNCTION creature_id_never_reused() RETURNS trigger AS $$
BEGIN
  IF EXISTS (SELECT 1 FROM creature_tombstones
             WHERE server_id = NEW.server_id AND creature_id = NEW.creature_id) THEN
    RAISE EXCEPTION 'creature id % was pruned and cannot be reused', NEW.creature_id;
  END IF;
  RETURN NEW;
END $$ LANGUAGE plpgsql;

-- DROP first, matching 0003's and 0004's idiom for this statement.
DROP TRIGGER IF EXISTS creatures_id_never_reused ON creatures;
-- INSERT *and* UPDATE OF creature_id, not INSERT alone. The guard is about
-- the ID SPACE, not about one statement shape: a trigger scoped to INSERT
-- is bypassed by a single UPDATE that moves a live creature onto a dead
-- one's id, which attaches the dead lineage exactly as effectively. The
-- column list is 0004's lesson applied at the point the trigger is written
-- rather than one migration later - every statement that could reach a
-- tombstoned id names creature_id, and no other statement enters plpgsql.
CREATE TRIGGER creatures_id_never_reused
  BEFORE INSERT OR UPDATE OF creature_id ON creatures
  FOR EACH ROW EXECUTE FUNCTION creature_id_never_reused();

-- design 3.3. One row per player: the region the Ark is parked in (a
-- constant this phase - there is one region) and three facility tiers.
-- PINNED, with no upgrade path; the facilities table is owed at design 11.
CREATE TABLE IF NOT EXISTS arks (
  server_id             integer  NOT NULL,
  player_id             uuid     NOT NULL,
  region_id             text     NOT NULL,
  harvest_array_tier    smallint NOT NULL DEFAULT 1,
  hatchery_tier         smallint NOT NULL DEFAULT 1,
  -- Tier 3, NOT 1, and not an inconsistency to be tidied away.
  -- combat_numbers 7 caps tiers 1-2 at G2 and therefore at Tier I coverage,
  -- which would make design 5.3's recessive downtier unreachable in the only
  -- configuration this phase ships - a rule nobody could observe. Tier 3
  -- caps at G4 and Tier II. Design 3.3 states the reasoning; the cost of the
  -- choice is one integer.
  splicing_chamber_tier smallint NOT NULL DEFAULT 3,
  PRIMARY KEY (server_id, player_id),
  FOREIGN KEY (server_id, player_id) REFERENCES players (server_id, player_id)
);

-- design 4.1/4.2: the node SET is derived by nodesFor() and never stored.
-- This table holds the only thing about a node that is not a pure function -
-- how much has been taken out of it this epoch. Keyed BY EPOCH, so the
-- boundary respawns the Rich Deposit at full yield by writing a new row
-- rather than by resetting an old one.
CREATE TABLE IF NOT EXISTS node_depletion (
  server_id       integer  NOT NULL,
  region_id       text     NOT NULL,
  node_slot       smallint NOT NULL,
  epoch           bigint   NOT NULL,
  harvested_units bigint   NOT NULL DEFAULT 0,
  depleted_at     timestamptz,
  PRIMARY KEY (server_id, region_id, node_slot, epoch),
  CONSTRAINT harvested_non_negative CHECK (harvested_units >= 0)
);

-- design 4.2: last_settled_at per player per node per epoch - the one input
-- accrue() takes that is not derivable.
CREATE TABLE IF NOT EXISTS harvest_positions (
  server_id       integer  NOT NULL,
  player_id       uuid     NOT NULL,
  region_id       text     NOT NULL,
  node_slot       smallint NOT NULL,
  epoch           bigint   NOT NULL,
  last_settled_at timestamptz NOT NULL,
  PRIMARY KEY (server_id, player_id, region_id, node_slot, epoch),
  FOREIGN KEY (server_id, player_id) REFERENCES players (server_id, player_id)
);

CREATE TABLE IF NOT EXISTS splices (
  server_id   integer NOT NULL,
  splice_id   uuid    NOT NULL DEFAULT gen_random_uuid(),
  player_id   uuid    NOT NULL,
  -- NO foreign key on parent_a/parent_b/child_id, deliberately. The splice
  -- CONSUMES both parents (design 5.5), so by the time this row is
  -- committed those creatures are gone or on their way to a tombstone, and
  -- the child may itself be pruned later. An FK here would make the splice
  -- record's own subject undeletable - the same interaction recorded
  -- against creatures.parent_a above, but here it is avoidable and avoided.
  parent_a    uuid    NOT NULL,
  parent_b    uuid    NOT NULL,
  child_id    uuid    NOT NULL,
  -- The roll is reproducible after the fact. design 5.1: a paid randomised
  -- action with published odds whose outcome cannot be re-derived has no
  -- evidence on either side of a dispute. Signed int8 against an unsigned
  -- engine seed, so the CHECK is the same one 0003 puts on wave_issuances -
  -- see that file for why a sign-flip is worse than an error.
  seed        bigint  NOT NULL,
  mutated     boolean NOT NULL,
  aberrant    boolean NOT NULL,
  created_at  timestamptz NOT NULL DEFAULT now(),
  PRIMARY KEY (server_id, splice_id),
  CONSTRAINT seed_non_negative CHECK (seed >= 0),
  FOREIGN KEY (server_id, player_id) REFERENCES players (server_id, player_id)
);
CREATE INDEX IF NOT EXISTS splices_by_player ON splices (server_id, player_id, created_at);

-- GRANTs. 0002's grant names five tables literally and cannot grow on its
-- own; forgetting this fails loudly (every query 403s) where forgetting RLS
-- below fails silently, which is why isolation.test.ts gates the latter and
-- not the former.
--
-- No DELETE except on creatures, and that one is design 3.2's prune. A
-- tombstone is permanent by definition, and a splice is a record of
-- something that happened - neither has a writer that should be able to
-- remove it, and 0003 declined DELETE on wave_issuances for the same reason.
GRANT SELECT, INSERT, UPDATE, DELETE ON creatures           TO broodline_app;
GRANT SELECT, INSERT                 ON creature_tombstones TO broodline_app;
GRANT SELECT, INSERT, UPDATE         ON arks                TO broodline_app;
GRANT SELECT, INSERT, UPDATE         ON node_depletion      TO broodline_app;
GRANT SELECT, INSERT, UPDATE         ON harvest_positions   TO broodline_app;
GRANT SELECT, INSERT                 ON splices             TO broodline_app;

-- RLS, matching 0002's shape exactly - including WITH CHECK, and including
-- current_setting's missing_ok argument so an unscoped query sees nothing
-- rather than raising. The isolation gate enumerates pg_class rather than a
-- list, so a table here that forgot either ENABLE or FORCE fails
-- isolation.test.ts without anyone updating that file.
DO $$
DECLARE t text;
BEGIN
  FOREACH t IN ARRAY ARRAY['creatures','creature_tombstones','arks',
                           'node_depletion','harvest_positions','splices']
  LOOP
    EXECUTE format('ALTER TABLE %I ENABLE ROW LEVEL SECURITY', t);
    EXECUTE format('ALTER TABLE %I FORCE  ROW LEVEL SECURITY', t);
    -- DROP first: CREATE POLICY has no IF NOT EXISTS, and this file is
    -- otherwise re-runnable (0003's M1 note).
    EXECUTE format('DROP POLICY IF EXISTS server_isolation ON %I', t);
    EXECUTE format($p$
      CREATE POLICY server_isolation ON %I
        FOR ALL
        USING      (server_id = NULLIF(current_setting('app.server_id', true), '')::int)
        WITH CHECK (server_id = NULLIF(current_setting('app.server_id', true), '')::int)
    $p$, t);
  END LOOP;
END $$;
