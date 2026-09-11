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

CREATE ROLE broodline_app NOLOGIN NOSUPERUSER NOBYPASSRLS;

GRANT SELECT, INSERT, UPDATE, DELETE ON
  players, wallets, ledger, idempotency_keys, campaign_progress
  TO broodline_app;

-- The two global tables are deliberately NOT under a policy. The account
-- record carries no server_id (5.1) and the registry describes servers
-- rather than living inside one.
GRANT SELECT, INSERT, UPDATE ON accounts TO broodline_app;
GRANT SELECT ON servers TO broodline_app;

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
