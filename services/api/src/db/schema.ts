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
import {
  bigint, index, integer, jsonb, pgEnum, pgTable, primaryKey,
  smallint, text, timestamp, uniqueIndex, uuid,
} from 'drizzle-orm/pg-core'

export const currency = pgEnum('currency', ['shards', 'splice_charges', 'marks', 'premium'])

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
