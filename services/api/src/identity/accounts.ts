import { and, eq, isNull } from 'drizzle-orm'
import type { Db, Tx } from '../db/client.ts'
import { accounts } from '../db/schema.ts'

/**
 * Gated on the CONSTRAINT, not just the SQLSTATE - the same discipline
 * money/idempotency.ts uses for idempotency_keys_pkey, and for the same
 * reason: a bare 23505 check would misread an unrelated unique violation
 * from inside the same statement as "this sub is already bound".
 */
function isAppleSubConflict(err: unknown): boolean {
  const e = err as { code?: string; constraint?: string } | null
  return typeof err === 'object' && e !== null
    && e.code === '23505' && e.constraint === 'accounts_apple_sub_key'
}

export interface NewAccount {
  birthdateBand: string
  storefrontRegion: string
}

/**
 * Server assignment, taken once at account creation and IMMUTABLE.
 *
 * solo_execution section 4: assignment is by storefront region at signup and
 * there are no transfers, ever. A player wanting to play elsewhere creates a
 * second unlinked account.
 *
 * NOT YET the guest-reroll defense section 4 describes. `storefrontRegion`
 * is a client-supplied request field (see routes/account.ts), and this
 * function is a total, deterministic function of it - so today a client
 * DOES choose its own server, simply by choosing what it sends as
 * storefrontRegion. The "cannot reroll onto a low-population server"
 * guarantee is not enforced by this code; it is unexploitable right now
 * only because REGION_TO_SERVER has exactly one entry and every other
 * region 400s. The guarantee becomes real only once storefrontRegion is
 * derived from a verified App Store receipt/storefront value instead of
 * trusted from the request body - out of scope for this task, required
 * before a second server opens.
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
 *
 * The WHERE clause requires the account to be UNBOUND (apple_sub IS NULL).
 * Without that, this silently REBINDS: an account that already holds sub 'X'
 * would have it overwritten by 'Y' with no check and no error, even though
 * the whole point of "bind" is that it targets a guest with nothing bound
 * yet. isNull is the guard against that.
 *
 * The uniqueness check is a caught constraint violation, not a pre-flight
 * SELECT. A SELECT-then-UPDATE has a race: two concurrent binds can both
 * pass the check and both issue the UPDATE, and only the constraint decides
 * who wins - the loser would then see a raw 23505 instead of this function's
 * error. Catching it here means the sequential and the concurrent path both
 * raise the IDENTICAL, friendly error.
 */
export async function bindApple(db: Db | Tx, accountId: string, sub: string): Promise<{ accountId: string }> {
  try {
    const [row] = await db.update(accounts).set({ appleSub: sub })
      .where(and(eq(accounts.accountId, accountId), isNull(accounts.appleSub)))
      .returning()
    if (row !== undefined) return { accountId: row.accountId }
  } catch (err) {
    if (isAppleSubConflict(err)) {
      // NEVER a merge. 6.4: the player is offered the account that already
      // holds this sub. Merging two rosters has no correct answer.
      throw new Error('This Apple ID is already bound to another account.')
    }
    throw err
  }

  // Zero rows updated by a WHERE that only ever matches an UNBOUND account:
  // either accountId does not exist, or it exists but already has an
  // apple_sub (possibly a different one). These are different failures and
  // must stay distinguishable - the first is a real 404, the second is an
  // attempted rebind that must be refused, not silently absorbed.
  const [existing] = await db.select().from(accounts).where(eq(accounts.accountId, accountId))
  if (existing === undefined) throw new Error('No such account.')
  throw new Error('This account already has an Apple ID bound; it cannot be rebound.')
}
