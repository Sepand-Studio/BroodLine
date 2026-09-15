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
export interface ReplayOpts { engineVersion?: string; trait?: string; tier?: number }

// --- Replay byte layout, mirroring engine/Runtime/Combat/Replay.cs's
// Serialize() field-for-field. Magic, format version and the field order
// below are NOT guessed - they are transcribed from that method, including
// its trailing-data rule (Deserialize rejects a record with even one extra
// byte, which is why nothing here is allowed to round up or pad).

const MAGIC = 0x50524c42 // "BLRP" little-endian, engine/Runtime/Combat/Replay.cs
const FORMAT_VERSION = 1
const DEFAULT_ENGINE_VERSION = '0.3.0' // engine/Runtime/SimVersion.cs SimVersion.Value

// engine/Runtime/Combat/Ids.cs
// Exported (with CREATURE_HP below) so base-stock.test.ts can pin the
// roster's granted HP against the ENGINE's numbers rather than against
// numbers retyped beside the grant - which would agree with themselves and
// with nothing else. This file is already the one place the engine's
// creature stats are mirrored on the TypeScript side.
export const SPECIES = { Vetch: 0, Ember: 1, Skitter: 2, Hollow: 3, Loam: 4, Pale: 5 } as const
const TRAIT = { None: 0, Chill: 1 } as const
const INSTINCT = { Vanguard: 1 } as const
const TERRAIN_DEFILE = 0 // engine/Runtime/Combat/Ids.cs Terrain.Defile

// engine/Runtime/Combat/Stats.cs CreatureHp
export const CREATURE_HP: Record<number, number> = {
  [SPECIES.Vetch]: 260, [SPECIES.Ember]: 130, [SPECIES.Skitter]: 80,
  [SPECIES.Hollow]: 60, [SPECIES.Loam]: 190, [SPECIES.Pale]: 120,
}

// engine/Runtime/Combat/Lane.cs Lane.Defile() - the only authored terrain
// family, so these are constants rather than per-wave data.
const LANE_TILES = 24
const POCKET_COUNT = 5

// engine/Runtime/Combat/WaveDef.cs Wave6(): laneCount 1. The only wave this
// bundle authors today (config/bundles/0.1.1/waves.json), so this is a
// one-entry table rather than a general lookup - it grows with content.
const WAVE_LANE_COUNT: Record<number, number> = { 6: 1 }

interface Creature {
  species: number; trait1: number; tier1: number; trait2: number; tier2: number
  instinct: number; pocket: number; hp: number
}

/**
 * Four Vetch holding the first four pockets, mirroring
 * tests/engine/Combat/GoldenTests.cs's DeploymentWithoutChill/WithChill - the
 * engine's own pinned fixture for wave 6, not a fixture reinvented here.
 */
function frontline(): Creature[] {
  return [0, 1, 2, 3].map((pocket) => ({
    species: SPECIES.Vetch, trait1: TRAIT.None, tier1: 0, trait2: TRAIT.None, tier2: 0,
    instinct: INSTINCT.Vanguard, pocket, hp: CREATURE_HP[SPECIES.Vetch]!,
  }))
}

function buildReplay(waveId: number, seed: bigint, last: Creature, o: ReplayOpts): string {
  const laneCount = WAVE_LANE_COUNT[waveId]
  if (laneCount === undefined) {
    throw new Error(`wave-helpers: no known lane count for wave ${waveId} - add it to WAVE_LANE_COUNT`)
  }
  const deployment = [...frontline(), last]
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
  buf.writeInt32LE(POCKET_COUNT, i); i += 4
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
export function buildWinningReplay(waveId: number, seed: bigint, o: ReplayOpts = {}): string {
  const trait = o.trait !== undefined ? TRAIT[o.trait as keyof typeof TRAIT] : TRAIT.Chill
  return buildReplay(waveId, seed, {
    species: SPECIES.Pale, trait1: trait, tier1: o.tier ?? 1, trait2: TRAIT.None, tier2: 0,
    instinct: INSTINCT.Vanguard, pocket: 4, hp: CREATURE_HP[SPECIES.Pale]!,
  }, o)
}

/**
 * Differs from the winning replay in one field: the fifth creature carries
 * Trait.None rather than Trait.Chill, so the Courser is unanswered and
 * breaches - Stats.CounterFor(Courser) is Chill and nothing else covers it.
 */
export function buildLosingReplay(waveId: number, seed: bigint, o: ReplayOpts = {}): string {
  return buildReplay(waveId, seed, {
    species: SPECIES.Loam, trait1: TRAIT.None, tier1: 0, trait2: TRAIT.None, tier2: 0,
    instinct: INSTINCT.Vanguard, pocket: 4, hp: CREATURE_HP[SPECIES.Loam]!,
  }, o)
}
