-- Two GLOBAL tables. data_model section 1 makes the Player global, and
-- solo_execution 5.1 keeps the account record small because it is the only
-- component that region-partitions when the first non-US market lands.

CREATE TABLE servers (
  server_id           integer PRIMARY KEY,
  region              text    NOT NULL,
  state               text    NOT NULL CHECK (state IN ('open', 'closed')),
  -- MINUTE granularity, not hour - solo_execution 9.7. Every server in a
  -- region ticking at the same minute makes the weekly tick simultaneously
  -- the heaviest scheduled job and the largest push fan-out in the game.
  -- Storing an hour and needing minutes later is a migration; storing
  -- minutes and only ever using :00 costs nothing.
  tick_day_of_week    smallint NOT NULL CHECK (tick_day_of_week BETWEEN 0 AND 6),
  tick_minute_of_day  smallint NOT NULL CHECK (tick_minute_of_day BETWEEN 0 AND 1439),
  opened_at           timestamptz NOT NULL DEFAULT now()
);

CREATE TABLE accounts (
  account_id      uuid PRIMARY KEY DEFAULT gen_random_uuid(),
  -- NULL for a guest. solo_execution 6.4: guests are real accounts with no
  -- credential bound, and upgrading binds the sub to the existing account so
  -- nothing migrates and there is no merge to conflict.
  apple_sub       text UNIQUE,
  birthdate_band  text NOT NULL,
  home_region     text NOT NULL,
  -- Assignment is by storefront region at signup and IMMUTABLE. There is no
  -- transfer path, ever - solo_execution section 4.
  server_id       integer NOT NULL REFERENCES servers(server_id),
  created_at      timestamptz NOT NULL DEFAULT now(),
  -- Soft delete. The App Store requires an in-app deletion path; the ledger
  -- is retained pseudonymised because it is a financial record - 6.4.
  deleted_at      timestamptz
);

-- Composite, not just account_id's own uniqueness: this is what lets
-- players below carry a composite FK of (server_id, account_id) rather than
-- account_id alone. account_id is already globally unique via the primary
-- key, so this index is free - it exists purely to give the composite FK
-- something to reference.
CREATE UNIQUE INDEX accounts_by_server ON accounts (server_id, account_id);

CREATE TYPE currency AS ENUM ('shards', 'splice_charges', 'marks', 'premium');

-- Five SERVER-SCOPED tables. server_id leads every primary key and index, so
-- a merge is a re-keying exercise and nothing is globally meaningful.

CREATE TABLE players (
  server_id   integer NOT NULL,
  player_id   uuid    NOT NULL DEFAULT gen_random_uuid(),
  account_id  uuid    NOT NULL,
  created_at  timestamptz NOT NULL DEFAULT now(),
  PRIMARY KEY (server_id, player_id),
  -- Composite, not account_id alone. A single-column FK only proves the
  -- account exists SOMEWHERE; it says nothing about which server it
  -- belongs to. Without this, RLS's WITH CHECK only validates
  -- players.server_id against the session, so a handler scoped to server 1
  -- could create a player row bound to a server 2 account - the account
  -- reference itself was never checked against the server.
  FOREIGN KEY (server_id, account_id) REFERENCES accounts (server_id, account_id)
);
CREATE UNIQUE INDEX players_by_account ON players (server_id, account_id);

CREATE TABLE wallets (
  server_id  integer  NOT NULL,
  player_id  uuid     NOT NULL,
  currency   currency NOT NULL,
  balance    bigint   NOT NULL CHECK (balance >= 0),
  -- Optimistic concurrency. solo_execution 5.4: compare-and-set, retry once,
  -- surface a conflict on the second failure. No locks.
  version    integer  NOT NULL DEFAULT 0,
  PRIMARY KEY (server_id, player_id, currency),
  -- Without this, a wallet can reference a player that does not exist -
  -- nearly free to add now, expensive once there are rows to reconcile.
  FOREIGN KEY (server_id, player_id) REFERENCES players (server_id, player_id)
);

CREATE TABLE ledger (
  server_id        integer  NOT NULL,
  -- uuid, not bigserial: a sequence is globally meaningful and would collide
  -- on a server merge.
  entry_id         uuid     NOT NULL DEFAULT gen_random_uuid(),
  player_id        uuid     NOT NULL,
  currency         currency NOT NULL,
  delta            bigint   NOT NULL,
  balance_after    bigint   NOT NULL,
  reason_code      text     NOT NULL,
  ref_type         text,
  ref_id           text,
  idempotency_key  text,
  created_at       timestamptz NOT NULL DEFAULT now(),
  PRIMARY KEY (server_id, entry_id),
  FOREIGN KEY (server_id, player_id) REFERENCES players (server_id, player_id)
);
CREATE INDEX ledger_by_player ON ledger (server_id, player_id, currency, created_at);

CREATE TABLE idempotency_keys (
  server_id     integer NOT NULL,
  key           text    NOT NULL,
  request_hash  text    NOT NULL,
  status        text    NOT NULL CHECK (status IN ('in_flight', 'completed')),
  response_body jsonb,
  created_at    timestamptz NOT NULL DEFAULT now(),
  PRIMARY KEY (server_id, key)
);
-- Rows age out after 24 hours - solo_execution 6.3.
CREATE INDEX idempotency_keys_by_age ON idempotency_keys (server_id, created_at);

CREATE TABLE campaign_progress (
  server_id            integer NOT NULL,
  player_id            uuid    NOT NULL,
  highest_wave_cleared integer NOT NULL DEFAULT 0,
  milestones_claimed   integer NOT NULL DEFAULT 0,
  updated_at           timestamptz NOT NULL DEFAULT now(),
  PRIMARY KEY (server_id, player_id),
  FOREIGN KEY (server_id, player_id) REFERENCES players (server_id, player_id)
);
