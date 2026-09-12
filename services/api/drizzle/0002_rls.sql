-- Isolation is Row-Level Security, per solo_execution 5.1. A query missing
-- its WHERE server_id returns ZERO rows rather than another server's data.
--
-- TWO things are needed and each defeats a different bypass:
--
--   FORCE ROW LEVEL SECURITY - without it the table OWNER is exempt, so a
--   suite connecting as the owner watches every policy do nothing while
--   reporting green.
--
--   A NON-SUPERUSER role - a superuser bypasses RLS entirely and FORCE does
--   not change that. Testcontainers' default user IS a superuser, so a suite
--   that skips this is testing nothing.

-- Roles are CLUSTER-scoped, not per-database like _migrations. A bare
-- CREATE ROLE fails with "role already exists" the moment a second database
-- (staging alongside prod, the normal arrangement on one instance) runs
-- this migration - so that environment could never be provisioned. Guard it
-- the same way _migrations guards a re-run, just at the right scope.
DO $$
BEGIN
  IF NOT EXISTS (SELECT FROM pg_roles WHERE rolname = 'broodline_app') THEN
    CREATE ROLE broodline_app NOLOGIN NOSUPERUSER NOBYPASSRLS;
  END IF;
END $$;

GRANT SELECT, INSERT, UPDATE, DELETE ON
  players, wallets, ledger, idempotency_keys, campaign_progress
  TO broodline_app;

-- accounts is deliberately NOT under an RLS policy. NOT because it lacks a
-- server_id - it has one, NOT NULL, referencing servers(server_id) - but
-- because the login path must resolve an account by apple_sub BEFORE it
-- knows which server to scope the session to. A policy here would make
-- sign-in itself impossible: the very first query of a session cannot pass
-- app.server_id, because that value is what the query exists to discover.
--
-- Because the table is unscoped, the grant on it has to do the work a
-- policy would otherwise do. UPDATE is narrowed to column level - only the
-- columns a handler legitimately rewrites, apple_sub (binding a guest's
-- credential) and deleted_at (soft delete) - rather than blanket UPDATE,
-- which would let a handler acting for ANY server rewrite ANY account's
-- apple_sub (an account-takeover primitive, not a read leak) or its
-- server_id, which 0001_tables.sql declares immutable and which nothing
-- enforced before the trigger below.
GRANT SELECT, INSERT ON accounts TO broodline_app;
GRANT UPDATE (apple_sub, deleted_at) ON accounts TO broodline_app;
GRANT SELECT ON servers TO broodline_app;

-- Enforced, not just declared in a comment. The column-level grant above
-- already keeps server_id unreachable for the app role, but a table OWNER
-- or superuser connection is not bound by grants at all - the same
-- exemption FORCE ROW LEVEL SECURITY closes for policies, this trigger
-- closes for the immutability claim, for every writer.
CREATE OR REPLACE FUNCTION reject_account_server_id_change() RETURNS trigger AS $$
BEGIN
  IF NEW.server_id IS DISTINCT FROM OLD.server_id THEN
    RAISE EXCEPTION 'accounts.server_id is immutable (attempted % -> %)', OLD.server_id, NEW.server_id;
  END IF;
  RETURN NEW;
END;
$$ LANGUAGE plpgsql;

CREATE TRIGGER accounts_server_id_immutable
  BEFORE UPDATE ON accounts
  FOR EACH ROW
  EXECUTE FUNCTION reject_account_server_id_change();

DO $$
DECLARE t text;
BEGIN
  FOREACH t IN ARRAY ARRAY['players','wallets','ledger','idempotency_keys','campaign_progress']
  LOOP
    EXECUTE format('ALTER TABLE %I ENABLE ROW LEVEL SECURITY', t);
    EXECUTE format('ALTER TABLE %I FORCE ROW LEVEL SECURITY', t);
    -- current_setting(..., true) returns NULL when unset instead of raising.
    -- NULL = server_id is NULL, never true, so an unscoped query sees
    -- nothing. That is default-deny, and it is the behaviour the isolation
    -- suite pins.
    EXECUTE format($p$
      CREATE POLICY server_isolation ON %I
        FOR ALL
        USING      (server_id = NULLIF(current_setting('app.server_id', true), '')::int)
        WITH CHECK (server_id = NULLIF(current_setting('app.server_id', true), '')::int)
    $p$, t);
  END LOOP;
END $$;
