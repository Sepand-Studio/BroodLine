/**
 * Typed query surface only - NOT the source of truth for the schema.
 *
 * drizzle/*.sql is authoritative: the CHECK constraints, every foreign key
 * (including the composite ones tying a server-scoped row to its parent on
 * the SAME server), and all of RLS live there and nowhere here. This file
 * mirrors table/column names and types so a query fails to compile the
 * moment it drifts from the SQL, but it enforces none of the above.
 *
 * There is no drizzle.config.ts wiring this file up to drizzle-kit today.
 * Keep it that way: running `drizzle-kit push` or `drizzle-kit generate`
 * from this file would silently produce a materially weaker schema - no
 * constraints, no RLS - with no warning that anything was lost.
 */
import { sql } from 'drizzle-orm'
import {
  bigint, boolean, customType, index, integer, jsonb, pgEnum, pgTable, primaryKey,
  smallint, text, timestamp, uniqueIndex, uuid,
} from 'drizzle-orm/pg-core'

export const currency = pgEnum('currency', ['shards', 'splice_charges', 'marks', 'premium'])

/**
 * int8 presented as a decimal string, both directions.
 *
 * drizzle-orm 0.38 offers no 'string' bigint mode - only 'number' (the
 * precision cliff above 2^53 this column exists to avoid) and 'bigint'
 * (exact, but a JS BigInt is what JSON.stringify throws on - and the seed
 * is serialised into a response body by both wave/start and wave/submit,
 * Tasks 5 and 6). A string is exact AND JSON-safe, and node-postgres
 * already hands int8 back as a string, so fromDriver is a normalisation
 * rather than a conversion.
 */
const int8String = customType<{ data: string; driverData: string }>({
  dataType: () => 'bigint',
  fromDriver: (v) => String(v),
  toDriver: (v) => v,
})

export const servers = pgTable('servers', {
  serverId: integer('server_id').primaryKey(),
  region: text('region').notNull(),
  state: text('state').notNull(),
  tickDayOfWeek: smallint('tick_day_of_week').notNull(),
  tickMinuteOfDay: smallint('tick_minute_of_day').notNull(),
  openedAt: timestamp('opened_at', { withTimezone: true }).notNull().defaultNow(),
})

export const accounts = pgTable('accounts', {
  accountId: uuid('account_id').primaryKey().defaultRandom(),
  appleSub: text('apple_sub'),
  birthdateBand: text('birthdate_band').notNull(),
  homeRegion: text('home_region').notNull(),
  serverId: integer('server_id').notNull(),
  createdAt: timestamp('created_at', { withTimezone: true }).notNull().defaultNow(),
  deletedAt: timestamp('deleted_at', { withTimezone: true }),
})

export const players = pgTable('players', {
  serverId: integer('server_id').notNull(),
  playerId: uuid('player_id').notNull().defaultRandom(),
  accountId: uuid('account_id').notNull(),
  createdAt: timestamp('created_at', { withTimezone: true }).notNull().defaultNow(),
}, (t) => ({
  pk: primaryKey({ columns: [t.serverId, t.playerId] }),
  byAccount: uniqueIndex('players_by_account').on(t.serverId, t.accountId),
}))

export const wallets = pgTable('wallets', {
  serverId: integer('server_id').notNull(),
  playerId: uuid('player_id').notNull(),
  currency: currency('currency').notNull(),
  // bigint as a JS number would silently lose precision past 2^53. mode
  // 'number' is chosen anyway because no balance in this game approaches it,
  // and the alternative poisons every arithmetic site with BigInt. If a
  // currency ever could, this is the line that changes.
  balance: bigint('balance', { mode: 'number' }).notNull(),
  version: integer('version').notNull().default(0),
}, (t) => ({
  pk: primaryKey({ columns: [t.serverId, t.playerId, t.currency] }),
}))

export const ledger = pgTable('ledger', {
  serverId: integer('server_id').notNull(),
  entryId: uuid('entry_id').notNull().defaultRandom(),
  playerId: uuid('player_id').notNull(),
  currency: currency('currency').notNull(),
  delta: bigint('delta', { mode: 'number' }).notNull(),
  balanceAfter: bigint('balance_after', { mode: 'number' }).notNull(),
  reasonCode: text('reason_code').notNull(),
  refType: text('ref_type'),
  refId: text('ref_id'),
  idempotencyKey: text('idempotency_key'),
  createdAt: timestamp('created_at', { withTimezone: true }).notNull().defaultNow(),
}, (t) => ({
  pk: primaryKey({ columns: [t.serverId, t.entryId] }),
  byPlayer: index('ledger_by_player').on(t.serverId, t.playerId, t.currency, t.createdAt),
}))

export const idempotencyKeys = pgTable('idempotency_keys', {
  serverId: integer('server_id').notNull(),
  key: text('key').notNull(),
  requestHash: text('request_hash').notNull(),
  status: text('status').notNull(),
  responseBody: jsonb('response_body'),
  createdAt: timestamp('created_at', { withTimezone: true }).notNull().defaultNow(),
}, (t) => ({
  pk: primaryKey({ columns: [t.serverId, t.key] }),
  byAge: index('idempotency_keys_by_age').on(t.serverId, t.createdAt),
}))

export const campaignProgress = pgTable('campaign_progress', {
  serverId: integer('server_id').notNull(),
  playerId: uuid('player_id').notNull(),
  highestWaveCleared: integer('highest_wave_cleared').notNull().default(0),
  milestonesClaimed: integer('milestones_claimed').notNull().default(0),
  updatedAt: timestamp('updated_at', { withTimezone: true }).notNull().defaultNow(),
}, (t) => ({
  pk: primaryKey({ columns: [t.serverId, t.playerId] }),
}))

export const waveIssuances = pgTable('wave_issuances', {
  serverId: integer('server_id').notNull(),
  issuanceId: uuid('issuance_id').notNull(),
  playerId: uuid('player_id').notNull(),
  waveId: integer('wave_id').notNull(),
  // int8 as a STRING via int8String, not bigint()'s built-in modes. seed is
  // a ulong in the engine and a JS number loses precision above 2^53 - the
  // same reason Task 2 sends the hash as a decimal string - which rules out
  // mode:'number'. mode:'bigint' is exact but returns a JS BigInt, which
  // JSON.stringify throws on, and this column is serialised into a response
  // body by both wave/start and wave/submit (Tasks 5, 6). (SQL adds
  // CHECK (seed >= 0) - bigint is signed and the engine's seed is not.)
  seed: int8String('seed').notNull(),
  issuedAt: timestamp('issued_at', { withTimezone: true }).notNull().defaultNow(),
  expiresAt: timestamp('expires_at', { withTimezone: true }).notNull(),
  // NULL while live. Set once, by trigger-enforced write-once, to the moment
  // the row stopped being live - never by the clock. design 4.3, amended
  // after review: a single consumed_at column and an index keyed on it being
  // NULL locked a player out on the abandoned-wave path, because expiry
  // cannot appear in a partial index's predicate (IMMUTABLE required, now()
  // is STABLE). settled_at/settlement together are the one terminal state
  // both the index and the handler's liveness check now share.
  settledAt: timestamp('settled_at', { withTimezone: true }),
  settlement: text('settlement'),
}, (t) => ({
  pk: primaryKey({ columns: [t.serverId, t.issuanceId] }),
  // design 4.3: the one-live-issuance-per-player rule, enforced by Postgres.
  // Mirrors 0003's wave_issuances_one_live exactly - keyed on settled_at,
  // not expiry.
  oneLive: uniqueIndex('wave_issuances_one_live').on(t.serverId, t.playerId)
    .where(sql`${t.settledAt} IS NULL`),
  // Mirrors 0003's wave_issuances_replay_count. Only 'consumed' rows count -
  // an 'expired' row was never played.
  replayCount: index('wave_issuances_replay_count').on(t.serverId, t.playerId, t.waveId, t.issuedAt)
    .where(sql`${t.settlement} = 'consumed'`),
}))

// --- 0005_loop.sql: the roster, the map's node state, and the splice record.

export const creatures = pgTable('creatures', {
  serverId: integer('server_id').notNull(),
  creatureId: uuid('creature_id').notNull().defaultRandom(),
  playerId: uuid('player_id').notNull(),
  species: text('species').notNull(),
  generation: integer('generation').notNull(),
  // Two combat TraitInstances as (trait, coverage_tier) pairs - data_model
  // 2, design 3.1. Slot 1 is the locked slot, slot 2 the rolled one.
  //
  // tier_N is NULLABLE and that is load-bearing: null means the slot holds
  // an Aberrant, which has no coverage. SQL adds
  // CHECK (tier_N IS NULL OR tier_N BETWEEN 1 AND 3) so zero can never be
  // stored - zero would sort and display as "less than tier I" and the two
  // must not be conflated. Nothing here enforces that; 0005 does.
  trait1: text('trait_1').notNull(),
  tier1: integer('tier_1'),
  trait2: text('trait_2').notNull(),
  tier2: integer('tier_2'),
  instinct: text('instinct').notNull(),
  // Founders only - SQL's only_founders_named.
  name: text('name'),
  isFounder: boolean('is_founder').notNull().default(false),
  // SQL ties these to creatures (server_id, creature_id) with a COMPOSITE
  // foreign key, so a parent on another server is unrepresentable rather
  // than merely wrong. A single uuid here says none of that.
  parentA: uuid('parent_a'),
  parentB: uuid('parent_b'),
  hpCurrent: integer('hp_current').notNull(),
  regenUntil: timestamp('regen_until', { withTimezone: true }),
  committedTo: uuid('committed_to'),
  acquiredAt: timestamp('acquired_at', { withTimezone: true }).notNull().defaultNow(),
}, (t) => ({
  pk: primaryKey({ columns: [t.serverId, t.creatureId] }),
  byPlayer: index('creatures_by_player').on(t.serverId, t.playerId, t.acquiredAt),
  // Partial, mirroring 0005: the roster screen and the Hatchery cap both ask
  // only about uncommitted creatures.
  available: index('creatures_available').on(t.serverId, t.playerId)
    .where(sql`${t.committedTo} IS NULL`),
}))

export const creatureTombstones = pgTable('creature_tombstones', {
  serverId: integer('server_id').notNull(),
  creatureId: uuid('creature_id').notNull(),
  species: text('species').notNull(),
  generation: integer('generation').notNull(),
  wasFounder: boolean('was_founder').notNull(),
}, (t) => ({
  pk: primaryKey({ columns: [t.serverId, t.creatureId] }),
}))

export const arks = pgTable('arks', {
  serverId: integer('server_id').notNull(),
  playerId: uuid('player_id').notNull(),
  regionId: text('region_id').notNull(),
  harvestArrayTier: smallint('harvest_array_tier').notNull().default(1),
  hatcheryTier: smallint('hatchery_tier').notNull().default(1),
  // 3, not 1 - design 3.3. Tiers 1-2 cap a creature at G2 and therefore at
  // Tier I coverage, which would leave design 5.3's recessive downtier with
  // nothing to drop to. The default lives in SQL; this mirror exists so a
  // reader of the typed surface does not "correct" it there either.
  splicingChamberTier: smallint('splicing_chamber_tier').notNull().default(3),
}, (t) => ({
  pk: primaryKey({ columns: [t.serverId, t.playerId] }),
}))

export const nodeDepletion = pgTable('node_depletion', {
  serverId: integer('server_id').notNull(),
  regionId: text('region_id').notNull(),
  nodeSlot: smallint('node_slot').notNull(),
  // The epoch comes from the server's tick fields - a small counter, not a
  // seed, so mode 'number' carries no precision risk. Same judgement (and
  // the same escape hatch) as wallets.balance.
  epoch: bigint('epoch', { mode: 'number' }).notNull(),
  harvestedUnits: bigint('harvested_units', { mode: 'number' }).notNull().default(0),
  depletedAt: timestamp('depleted_at', { withTimezone: true }),
}, (t) => ({
  pk: primaryKey({ columns: [t.serverId, t.regionId, t.nodeSlot, t.epoch] }),
}))

export const harvestPositions = pgTable('harvest_positions', {
  serverId: integer('server_id').notNull(),
  playerId: uuid('player_id').notNull(),
  regionId: text('region_id').notNull(),
  nodeSlot: smallint('node_slot').notNull(),
  epoch: bigint('epoch', { mode: 'number' }).notNull(),
  lastSettledAt: timestamp('last_settled_at', { withTimezone: true }).notNull(),
}, (t) => ({
  pk: primaryKey({ columns: [t.serverId, t.playerId, t.regionId, t.nodeSlot, t.epoch] }),
}))

export const splices = pgTable('splices', {
  serverId: integer('server_id').notNull(),
  spliceId: uuid('splice_id').notNull().defaultRandom(),
  playerId: uuid('player_id').notNull(),
  // No FK in SQL either - the splice consumes both parents, so an FK would
  // make the record's own subject undeletable.
  parentA: uuid('parent_a').notNull(),
  parentB: uuid('parent_b').notNull(),
  childId: uuid('child_id').notNull(),
  // int8String, exactly as waveIssuances.seed: the roll is reproducible
  // after the fact (design 5.1) and the value is serialised into the
  // splice/commit response, where a JS BigInt is what JSON.stringify throws
  // on and a JS number loses precision above 2^53. (SQL adds
  // CHECK (seed >= 0) - bigint is signed and the seed is not.)
  seed: int8String('seed').notNull(),
  mutated: boolean('mutated').notNull(),
  aberrant: boolean('aberrant').notNull(),
  createdAt: timestamp('created_at', { withTimezone: true }).notNull().defaultNow(),
}, (t) => ({
  pk: primaryKey({ columns: [t.serverId, t.spliceId] }),
  byPlayer: index('splices_by_player').on(t.serverId, t.playerId, t.createdAt),
}))
