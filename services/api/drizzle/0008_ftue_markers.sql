-- Three write-once instants on campaign_progress. Each records that a
-- one-time FTUE grant has fired, so the grant is idempotent by a row the
-- player cannot influence rather than by counting creatures (a splice could
-- remove the very creature the count relied on).
--
-- EXPAND ONLY. Nullable, no default, no reader in this deploy -
-- solo_execution 7.0. IF NOT EXISTS for the reason 0006 gives.
ALTER TABLE campaign_progress ADD COLUMN IF NOT EXISTS founder_granted_at        timestamptz;
ALTER TABLE campaign_progress ADD COLUMN IF NOT EXISTS tutorial_stock_granted_at timestamptz;
ALTER TABLE campaign_progress ADD COLUMN IF NOT EXISTS wave6_pale_granted_at     timestamptz;
