/**
 * The replay wire format, mirroring engine/Runtime/Combat/Replay.cs's
 * Serialize() field-for-field.
 *
 * WHY THIS IS ITS OWN MODULE. It is pure byte-packing and depends on nothing
 * else in this package. While it lived in wave-helpers.ts, importing it also
 * imported the Hono app, the Drizzle schema and SimClient - fine for a test
 * that wants all of those, fatal for a process that wants only a replay.
 * implementation/scripts/smoke-wave.ts is exactly that process: it drives a
 * DEPLOYED api over HTTP and needs no local app at all. It also could not
 * load the old graph, because SimClient's constructor uses TypeScript
 * parameter properties, which `node --experimental-strip-types` refuses.
 *
 * The split also puts the boundary where it belongs: the byte layout is owned
 * by the engine, not by the HTTP surface, and now says so.
 *
 * wave-helpers.ts re-exports everything here, so existing importers are
 * unaffected and there is still exactly one definition of the format.
 */
import { readFileSync } from 'node:fs'
import { join } from 'node:path'
import { fileURLToPath } from 'node:url'

export interface ReplayOpts { engineVersion?: string; trait?: string; tier?: number }

/**
 * A creature AS THE REPLAY CARRIES IT - engine ordinals, because that is what
 * the bytes hold. `asRosterSpecs` below is the only place this is translated
 * into the shape `api` stores, and it translates in the ONE direction `sim`
 * itself translates (ordinal -> name, 0 -> null).
 */
export interface ReplayCreature {
  species: number; trait1: number; tier1: number; trait2: number; tier2: number
  instinct: number; pocket: number; hp: number
}

/**
 * The same creature in the shape `creatures` stores it, plus the hit points a
 * row carries and a spec does not.
 *
 * Structurally `services/api/src/db/schema.ts`'s `CreatureSpec` with `hp`
 * added - deliberately not imported from there, because importing the schema
 * would drag Drizzle back into this module and undo the split this file's
 * header describes.
 */
export interface RosterSpec {
  species: string
  trait1: string; tier1: number | null
  trait2: string; tier2: number | null
  instinct: string
  pocket: number
  hp: number
}

// --- Replay byte layout, mirroring engine/Runtime/Combat/Replay.cs's
// Serialize() field-for-field. Magic, format version and the field order
// below are NOT guessed - they are transcribed from that method, including
// its trailing-data rule (Deserialize rejects a record with even one extra
// byte, which is why nothing here is allowed to round up or pad).

const MAGIC = 0x50524c42 // "BLRP" little-endian, engine/Runtime/Combat/Replay.cs
const FORMAT_VERSION = 1

// fileURLToPath, not .pathname - this repo's own directory contains a space,
// same as base-stock.test.ts and every other file here that resolves REPO.
const REPO = fileURLToPath(new URL('../../../', import.meta.url))

/**
 * DERIVED from engine/Runtime/SimVersion.cs, not retyped beside it.
 *
 * This WAS a hand-maintained '0.3.0' literal, and Task 1's bump to engine
 * 0.4.0 (waves 1 and 2 owning their lane) left it behind: every replay this
 * module built kept claiming engine 0.3.0, `sim` correctly refused every one
 * of them with engine_too_old, and 41 tests across wave-submit, replays, loop
 * and adversarial failed for a reason that had nothing to do with what they
 * were testing.
 *
 * tests/engine/Combat/DeviceReplayTests.cs's ReplayArtifact.CapturedUnder
 * made the identical fix for the identical reason, in its own words: "a
 * constant that duplicates a fact already in the file is a claim with a
 * maintenance cost and no enforcement." There the fact lives in a captured
 * artifact's bytes; here it lives in SimVersion.cs's source text, which is
 * the only copy of it reachable from a TypeScript process without shelling
 * out to dotnet - so it is read and parsed rather than re-typed.
 *
 * Throws rather than falling back to a literal on a parse miss: a silent
 * default here is exactly the failure mode this replaces, just one commit
 * further from where anyone would think to look for it.
 */
function readEngineVersionFromEngineSource(): string {
  const path = join(REPO, 'engine/Runtime/SimVersion.cs')
  const src = readFileSync(path, 'utf8')
  const version = /public const string Value = "([^"]+)"/.exec(src)?.[1]
  if (version === undefined) {
    throw new Error(
      `replay-format.ts: expected to find 'public const string Value = "X.Y.Z"' in ${path}, ` +
      'but no such line matched. SimVersion.cs\'s shape changed and this parser needs to move ' +
      'with it - see the comment on readEngineVersionFromEngineSource.',
    )
  }
  return version
}

const DEFAULT_ENGINE_VERSION = readEngineVersionFromEngineSource()

// engine/Runtime/Combat/Ids.cs
// Exported (with CREATURE_HP below) so base-stock.test.ts can pin the
// roster's granted HP against the ENGINE's numbers rather than against
// numbers retyped beside the grant - which would agree with themselves and
// with nothing else. This file is already the one place the engine's
// creature stats are mirrored on the TypeScript side.
export const SPECIES = { Vetch: 0, Ember: 1, Skitter: 2, Hollow: 3, Loam: 4, Pale: 5 } as const
// engine/Runtime/Combat/Ids.cs's Trait enum, in full - Task 6 (Phase 7) needs
// Taunt/Splash/Carapace for the cold-open pair and the wave-2 trio, where
// earlier tasks only ever needed Chill.
//
// Exported for the same reason SPECIES and CREATURE_HP are: Task 22's
// `ftue.test.ts` has to build a replay of creatures the SERVER minted - the
// wave-6 Pale carries `Chill`/1 AND `Carapace`/1 (src/ftue/pale.ts's
// WAVE6_PALE), which no authored deployment in this file describes - and the
// alternative is a second copy of the enum's ordinals in a test file, which
// would agree with itself and with nothing else.
export const TRAIT = { None: 0, Chill: 1, Taunt: 2, Splash: 3, Carapace: 4 } as const
const INSTINCT = { Vanguard: 1 } as const
const TERRAIN_DEFILE = 0 // engine/Runtime/Combat/Ids.cs Terrain.Defile

// engine/Runtime/Combat/Stats.cs CreatureHp
export const CREATURE_HP: Record<number, number> = {
  [SPECIES.Vetch]: 260, [SPECIES.Ember]: 130, [SPECIES.Skitter]: 80,
  [SPECIES.Hollow]: 60, [SPECIES.Loam]: 190, [SPECIES.Pale]: 120,
}

// engine/Runtime/Combat/Lane.cs's two authored Defile layouts, keyed by wave
// id - NOT one constant, since Task 6 (Phase 7) is the first caller to build
// a replay for wave 1 or 2, and those run on `Lane.DefileSix()` (six
// pockets), while waves 6 and 7 run on `Lane.Defile()` (five). `sim` checks
// the replay's own `PocketCount` field against the wave's actual lane
// (Replay.cs's Validate: "pocket count N disagrees with Defile's M") and
// throws a ReplayFormatException on a mismatch, so this is not cosmetic - a
// wave-1 replay built with the wrong count never reaches combat resolution.
// LANE_TILES is one constant because both layouts share
// `Stats.LaneTiles` (24) - only the pocket geometry differs between them.
const LANE_TILES = 24
const WAVE_GEOMETRY: Record<number, { laneCount: number; pocketCount: number }> = {
  1: { laneCount: 1, pocketCount: 6 }, // Lane.DefileSix() - WaveDef.Wave1()
  2: { laneCount: 1, pocketCount: 6 }, // Lane.DefileSix() - WaveDef.Wave2()
  6: { laneCount: 1, pocketCount: 5 }, // Lane.Defile() - WaveDef.Wave6()
  7: { laneCount: 1, pocketCount: 5 }, // Lane.Defile() - WaveDef.Wave7()
}

/**
 * Four Vetch holding the first four pockets, mirroring
 * tests/engine/Combat/GoldenTests.cs's DeploymentWithoutChill/WithChill - the
 * engine's own pinned fixture for wave 6, not a fixture reinvented here.
 */
function frontline(): ReplayCreature[] {
  return [0, 1, 2, 3].map((pocket) => ({
    species: SPECIES.Vetch, trait1: TRAIT.None, tier1: 0, trait2: TRAIT.None, tier2: 0,
    instinct: INSTINCT.Vanguard, pocket, hp: CREATURE_HP[SPECIES.Vetch]!,
  }))
}

function buildReplay(waveId: number, seed: bigint, deployment: readonly ReplayCreature[], o: ReplayOpts): string {
  const geometry = WAVE_GEOMETRY[waveId]
  if (geometry === undefined) {
    throw new Error(`wave-helpers: no known lane geometry for wave ${waveId} - add it to WAVE_GEOMETRY`)
  }
  const { laneCount, pocketCount } = geometry
  const engineVersion = o.engineVersion ?? DEFAULT_ENGINE_VERSION
  const versionBytes = Buffer.from(engineVersion, 'utf8')

  const size = 4 + 2 + 1 + versionBytes.length
    + 4 + 8 + 4 + 4 + 4 + 4
    + 4 + deployment.length * 8 * 4
    + 4 + 4
  const buf = Buffer.alloc(size)
  let i = 0

  buf.writeUInt32LE(MAGIC, i); i += 4
  buf.writeUInt16LE(FORMAT_VERSION, i); i += 2
  buf.writeUInt8(versionBytes.length, i); i += 1
  versionBytes.copy(buf, i); i += versionBytes.length

  buf.writeInt32LE(waveId, i); i += 4
  buf.writeBigUInt64LE(seed, i); i += 8
  buf.writeInt32LE(TERRAIN_DEFILE, i); i += 4
  buf.writeInt32LE(laneCount, i); i += 4
  buf.writeInt32LE(pocketCount, i); i += 4
  buf.writeInt32LE(LANE_TILES, i); i += 4

  buf.writeInt32LE(deployment.length, i); i += 4
  for (const c of deployment) {
    buf.writeInt32LE(c.species, i); i += 4
    buf.writeInt32LE(c.trait1, i); i += 4
    buf.writeInt32LE(c.tier1, i); i += 4
    buf.writeInt32LE(c.trait2, i); i += 4
    buf.writeInt32LE(c.tier2, i); i += 4
    buf.writeInt32LE(c.instinct, i); i += 4
    buf.writeInt32LE(c.pocket, i); i += 4
    buf.writeInt32LE(c.hp, i); i += 4
  }

  buf.writeInt32LE(-1, i); i += 4 // RallyTick, absent
  buf.writeInt32LE(-1, i); i += 4 // RallyCreature, absent

  return buf.toString('base64')
}

/** The fifth creature carries Chill, which answers wave 6's lone Courser. */
export function winningDeployment(o: ReplayOpts = {}): ReplayCreature[] {
  const trait = o.trait !== undefined ? TRAIT[o.trait as keyof typeof TRAIT] : TRAIT.Chill
  return [...frontline(), {
    species: SPECIES.Pale, trait1: trait, tier1: o.tier ?? 1, trait2: TRAIT.None, tier2: 0,
    instinct: INSTINCT.Vanguard, pocket: 4, hp: CREATURE_HP[SPECIES.Pale]!,
  }]
}

/**
 * Differs from the winning deployment in one field: the fifth creature
 * carries Trait.None rather than Trait.Chill, so the Courser is unanswered
 * and breaches - Stats.CounterFor(Courser) is Chill and nothing else covers
 * it.
 */
export function losingDeployment(): ReplayCreature[] {
  return [...frontline(), {
    species: SPECIES.Loam, trait1: TRAIT.None, tier1: 0, trait2: TRAIT.None, tier2: 0,
    instinct: INSTINCT.Vanguard, pocket: 4, hp: CREATURE_HP[SPECIES.Loam]!,
  }]
}

/**
 * starter.json's cold-open pair, exactly as task-1-brief.md's
 * `WaveContentTests.ColdOpenPair()` authors it and task-1-report.md confirms
 * wins wave 1 at EVERY ordered pocket pair on `Lane.DefileSix()`. Task 6
 * (Phase 7) is the first caller to submit a real wave-1 replay over HTTP;
 * pockets 0 and 2 match Task 17's tutorial placement, not a requirement of
 * the wave itself.
 */
export function coldOpenDeployment(): ReplayCreature[] {
  return [
    {
      species: SPECIES.Vetch, trait1: TRAIT.Taunt, tier1: 1, trait2: TRAIT.Carapace, tier2: 1,
      instinct: INSTINCT.Vanguard, pocket: 0, hp: CREATURE_HP[SPECIES.Vetch]!,
    },
    {
      species: SPECIES.Ember, trait1: TRAIT.Splash, tier1: 1, trait2: TRAIT.Carapace, tier2: 1,
      instinct: INSTINCT.Vanguard, pocket: 2, hp: CREATURE_HP[SPECIES.Ember]!,
    },
  ]
}

/**
 * Beat 5's trio, exactly as task-1-brief.md's `Wave2_IsWonByTheTrio_WithTauntOnTheVetch`
 * authors it: the cold-open pair, plus the Founder Hollow (no traits, minted
 * by Task 6's own `grantFounder`) holding the back pocket - "the Vetch holds
 * the Lash because Taunt is on it".
 */
export function wave2TrioDeployment(): ReplayCreature[] {
  return [
    ...coldOpenDeployment(),
    {
      species: SPECIES.Hollow, trait1: TRAIT.None, tier1: 0, trait2: TRAIT.None, tier2: 0,
      instinct: INSTINCT.Vanguard, pocket: 4, hp: CREATURE_HP[SPECIES.Hollow]!,
    },
  ]
}

/**
 * A replay of an ARBITRARY deployment - what the two builders below are, with
 * the deployment named rather than implied.
 *
 * Exported for the submit-side comparison's tests: the only way to show that
 * `api` compares the echoed deployment against the ISSUED one is to submit a
 * replay claiming a different one, and a builder whose deployment is fixed
 * cannot express that.
 */
export function buildReplayOf(
  waveId: number, seed: bigint, deployment: readonly ReplayCreature[], o: ReplayOpts = {},
): string {
  return buildReplay(waveId, seed, deployment, o)
}

export function buildWinningReplay(waveId: number, seed: bigint, o: ReplayOpts = {}): string {
  return buildReplay(waveId, seed, winningDeployment(o), o)
}

export function buildLosingReplay(waveId: number, seed: bigint, o: ReplayOpts = {}): string {
  return buildReplay(waveId, seed, losingDeployment(), o)
}

// --- The one translation from the replay's ordinals to the roster's names.
//
// THIS IS A MIRROR OF `sim`'s OWN MAPPING (services/sim/SimulateEndpoint.cs:
// `d.Trait1.ToString()` and `Coverage(tier)`), and it exists so a test can
// mint the creatures a given replay CLAIMS - which is the only way to build an
// issuance whose stored deployment the echo can legitimately match.
//
// IT IS SAFE TO MIRROR HERE AND NOT IN `src/`. The mapping in `src/` would be
// the wrong direction (name -> ordinal), and its unknown-name case has no
// honest answer: `None` is the obvious default and the zero value, so a forged
// 'Bogus' would compare equal to a simulated Trait.None slot. Here the
// direction is the same one `sim` takes, the input is a declared member by
// construction, and a mistake in it cannot make the comparison pass - it makes
// the honest-submission tests go RED, loudly.
const SPECIES_NAME = invert(SPECIES)
const TRAIT_NAME = invert(TRAIT)
const INSTINCT_NAME = invert(INSTINCT)

function invert(table: Record<string, number>): Record<number, string> {
  return Object.fromEntries(Object.entries(table).map(([name, n]) => [n, name]))
}

function nameOf(table: Record<number, string>, n: number, what: string): string {
  const name = table[n]
  if (name === undefined) throw new Error(`replay-format: no ${what} name for ordinal ${n}`)
  return name
}

/**
 * The creatures a player would have to OWN for the given replay to be an
 * honest one.
 *
 * `tier 0 -> null` is `sim`'s `Coverage()` restated: the engine spells "this
 * trait is not really carried" as 0 and `api` spells it null, because
 * 0005_loop.sql's coverage_tier_N_not_zero makes 0 unstorable.
 */
export function asRosterSpecs(deployment: readonly ReplayCreature[]): RosterSpec[] {
  return deployment.map((c) => ({
    species: nameOf(SPECIES_NAME, c.species, 'species'),
    trait1: nameOf(TRAIT_NAME, c.trait1, 'trait'),
    tier1: c.tier1 === 0 ? null : c.tier1,
    trait2: nameOf(TRAIT_NAME, c.trait2, 'trait'),
    tier2: c.tier2 === 0 ? null : c.tier2,
    instinct: nameOf(INSTINCT_NAME, c.instinct, 'instinct'),
    pocket: c.pocket,
    hp: c.hp,
  }))
}
