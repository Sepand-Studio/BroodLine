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
-- THERE IS NO SECOND TABLE FOR PRUNED CREATURES. design 3.2, amended during
-- execution (specs commit f66834d): tombstones-in-their-own-table and
-- composite parent foreign keys back onto creatures are mutually exclusive,
-- and the design did not notice. Every ancestor is referenced by its own
-- child and the keys are NO ACTION, so DELETE on any prunable row violates
-- its own child's key - the prune could delete nothing at all, and
-- data_model 4's nine-thousand-rows-per-player problem would have gone
-- unsolved while looking solved. Measured, not argued, before the ruling.
--
-- Resolved by removing the second table rather than by weakening the keys.
-- Pruning is an UPDATE: it nulls a creature down to
-- {species, generation, is_founder} and sets `pruned`. The keys stay
-- declarative and planner-enforced, the lineage view resolves through them
-- to a row that really exists, and ID REUSE IS IMPOSSIBLE BY CONSTRUCTION
-- because the row never goes away - which retired the reuse trigger this
-- file used to carry.
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
  -- NULLABLE, and only because of the prune: a pruned row is nulled down to
  -- {species, generation, is_founder}, so these four cannot carry a
  -- column-level NOT NULL. live_creatures_are_whole below restores exactly
  -- that guarantee for every row that is not pruned, so a LIVE creature
  -- missing a trait is still refused - by a CHECK instead of by the column.
  trait_1        text,
  tier_1         integer,
  trait_2        text,
  tier_2         integer,
  instinct       text,
  name           text,
  -- NOT NULL, so the prune cannot null it even by accident. design 3.2
  -- retains Founders permanently and this flag is the only thing that
  -- distinguishes one afterwards; founders_are_never_pruned below says the
  -- rest of that sentence.
  is_founder     boolean NOT NULL DEFAULT false,
  parent_a       uuid,
  parent_b       uuid,
  hp_current     integer,
  regen_until    timestamptz,
  committed_to   uuid,
  acquired_at    timestamptz NOT NULL DEFAULT now(),
  -- design 3.2's tombstone, in place. A pruned row keeps its identity
  -- (server_id, creature_id), its parent pointers, and the three fields a
  -- lineage view renders - "an unnamed Gen-4 Vetch" - and nothing else.
  pruned         boolean NOT NULL DEFAULT false,

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
  -- data_model 2: only Founders may be named. A pruned row nulls `name`, so
  -- this holds for one trivially - checked, not assumed (see the test).
  CONSTRAINT only_founders_named CHECK (name IS NULL OR is_founder),
  -- What the column-level NOT NULLs above used to say, restricted to rows
  -- the prune has not stripped. Without this, dropping those NOT NULLs to
  -- make the prune expressible would ALSO have made a live creature with no
  -- trait and no instinct insertable, which is a strictly worse schema than
  -- the one the ruling replaced.
  CONSTRAINT live_creatures_are_whole CHECK (
    pruned OR (trait_1 IS NOT NULL AND trait_2 IS NOT NULL
               AND instinct IS NOT NULL AND hp_current IS NOT NULL)),
  -- design 3.2: "Retain all Founders permanently." Pruning a Founder would
  -- null the name bible 3.3 makes the emotional anchor of every descendant's
  -- tree, and only_founders_named means it could never be written back.
  CONSTRAINT founders_are_never_pruned CHECK (NOT (pruned AND is_founder)),

  FOREIGN KEY (server_id, player_id) REFERENCES players (server_id, player_id),

  -- COMPOSITE, so a parent on another server is not merely wrong but
  -- UNREPRESENTABLE. A single-column FK would only prove the parent exists
  -- SOMEWHERE - 0001 makes the same argument for players.account_id, before
  -- money landed on it.
  --
  -- NO ACTION, deliberately, and this is the settled end of the collision
  -- the header describes. A creature that is any creature's parent cannot be
  -- deleted - which is now correct rather than obstructive, because the
  -- prune is an UPDATE and needs no delete at all.
  --
  -- ON DELETE CASCADE and ON DELETE SET NULL are both WRONG here and a
  -- future reader should not "fix" this by adding either. CASCADE would take
  -- the living descendant with the ancestor; SET NULL would blank the
  -- child's pointer, so the lineage view could no longer reach the very row
  -- the prune exists to preserve. `refuses to DELETE a creature that is
  -- still someone's parent` reddens for both.
  FOREIGN KEY (server_id, parent_a)  REFERENCES creatures (server_id, creature_id),
  FOREIGN KEY (server_id, parent_b)  REFERENCES creatures (server_id, creature_id)
);

-- BOTH indexes are partial on `NOT pruned`, and that is a direct consequence
-- of the ruling rather than a tidy-up. While tombstones lived in their own
-- table these indexes were live-only for free; with pruned rows in THIS
-- table every index carries them unless told not to.
--
-- For creatures_by_player that is a size argument: data_model 4 puts the
-- pruned population at roughly nine thousand rows per player over two years
-- against a live roster the Hatchery caps at twenty, so an unfiltered index
-- would be ~99% dead entries that no roster query ever wants.
--
-- For creatures_available the predicate ALSO carries a warning, and it is
-- worth stating precisely rather than dramatically. A pruned row has
-- committed_to nulled, so `committed_to IS NULL` is TRUE of it: the test
-- `the availability predicate must say NOT pruned` demonstrates that
-- directly. A Hatchery-cap query written as `committed_to IS NULL` alone -
-- the predicate this index carried before the ruling - therefore counts
-- every dead ancestor against the player's roster cap.
--
-- The index does not PREVENT that; only the query's own WHERE can, and
-- Tasks 5 and 7 own those queries. What the predicate does is keep the
-- index's definition and the meaning of "available" in one place, so a
-- query that forgets `NOT pruned` loses the index rather than matching it.
-- That is an access-path argument, not a correctness one, and it is why no
-- test reddens when this predicate alone is weakened - recorded as a
-- finding in the task report rather than papered over with a test that
-- would only be re-asserting the query it already wrote.
CREATE INDEX IF NOT EXISTS creatures_by_player ON creatures (server_id, player_id, acquired_at)
  WHERE NOT pruned;
CREATE INDEX IF NOT EXISTS creatures_available ON creatures (server_id, player_id)
  WHERE committed_to IS NULL AND NOT pruned;

-- NO creature_tombstones TABLE, and no creature_id_never_reused trigger.
-- Both were in this file and both are gone - see the header. The tombstone
-- is the `pruned` row above, and id reuse is not guarded because it is not
-- POSSIBLE: a pruned creature keeps its primary key, so a second creature
-- claiming that id collides with the row itself.
--
-- Worth keeping on the record, because it was measured rather than reasoned
-- about: the trigger this replaces shipped as
-- `BEFORE INSERT OR UPDATE OF creature_id`, widened from the brief's
-- `BEFORE INSERT` after a weakening showed a single UPDATE could move a live
-- creature onto a dead one's id and attach its lineage. That scope was the
-- right fix for the design as it then stood. It is moot now - the primary
-- key does the whole job, for both statement shapes, with no plpgsql
-- entered on any write.

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
  -- NO foreign key on parent_a/parent_b/child_id - and the reason is no
  -- longer the one this comment first gave. That reason was "the splice
  -- consumes both parents, so those rows are gone by the time this one is
  -- committed", which the amended design 3.2 makes FALSE: a consumed parent
  -- is pruned, not deleted, so all three ids now name rows that still exist
  -- and composite FKs here WOULD be satisfiable and would make a
  -- cross-server splice unrepresentable, exactly as they do on creatures.
  --
  -- Left unadded because Task 7 owns the splice write path and adding them
  -- constrains its statement order; recorded as OWED rather than settled, so
  -- the next writer decides it deliberately instead of inheriting a stale
  -- justification.
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
-- NO DELETE ANYWHERE, creatures included. The first version of this file
-- granted DELETE on creatures because design 3.2's prune was a delete; under
-- the amended 3.2 the prune is an UPDATE, so the grant lost its only
-- justification and is withdrawn rather than left lying around. A handler
-- that could delete a creature could break a living descendant's lineage,
-- which is exactly what the parent keys above exist to prevent - and 0003
-- declined DELETE on wave_issuances on the same reasoning.
GRANT SELECT, INSERT, UPDATE         ON creatures           TO broodline_app;
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
  FOREACH t IN ARRAY ARRAY['creatures','arks',
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
