import { eq } from 'drizzle-orm'
import type { Db, Tx } from '../db/client.ts'
import { accounts } from '../db/schema.ts'

export interface NewAccount {
  birthdateBand: string
  storefrontRegion: string
}

/**
 * Server assignment, taken once at account creation and IMMUTABLE.
 *
 * solo_execution section 4: assignment is by storefront region at signup and
 * there are no transfers, ever. A player wanting to play elsewhere creates a
 * second unlinked account. That is also what closes the guest-reroll hole -
 * assignment is not something a client can influence, so a guest cannot
 * reroll onto a low-population server to farm its Apex Veins.
 *
 * ONE SERVER AT MILESTONE 1. The mapping is a function rather than a constant
 * so the shape is right when the second one opens; solo_execution section 4
 * defers the population-trigger opening to that point.
 */
export async function assignServer(storefrontRegion: string): Promise<number> {
  const REGION_TO_SERVER: Record<string, number> = { 'us-central1': 1 }
  const serverId = REGION_TO_SERVER[storefrontRegion]
  if (serverId === undefined) {
    throw new Error(`No server serves storefront region ${storefrontRegion}.`)
  }
  return serverId
}

export async function createGuest(db: Db | Tx, a: NewAccount): Promise<{ accountId: string; serverId: number }> {
  const serverId = await assignServer(a.storefrontRegion)
  const [row] = await db.insert(accounts).values({
    appleSub: null,
    birthdateBand: a.birthdateBand,
    homeRegion: a.storefrontRegion,
    serverId,
  }).returning()
  return { accountId: row!.accountId, serverId }
}

/**
 * Binds an Apple sub to an existing account. No data moves, because there is
 * nothing to move - the guest was always a real account.
 */
export async function bindApple(db: Db | Tx, accountId: string, sub: string): Promise<{ accountId: string }> {
  const [existing] = await db.select().from(accounts).where(eq(accounts.appleSub, sub))
  if (existing !== undefined && existing.accountId !== accountId) {
    // NEVER a merge. 6.4: the player is offered the account that already
    // holds this sub. Merging two rosters has no correct answer.
    throw new Error('This Apple ID is already bound to another account.')
  }

  const [row] = await db.update(accounts).set({ appleSub: sub })
    .where(eq(accounts.accountId, accountId)).returning()
  if (row === undefined) throw new Error('No such account.')
  return { accountId: row.accountId }
}
