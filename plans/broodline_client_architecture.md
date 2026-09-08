# Broodline — Client Architecture

*Technical spec, how the Unity client is put together*

> **PROPOSED — not yet in the supersession map.** Owns the client's layering,
> the render budget, runtime creature assembly, content packaging, local
> persistence, the offline model and build configuration. Does not own the
> simulation (`broodline_combat_engine.md`), the socket standard
> (`broodline_rig_proof.md`), screen contents (`broodline_screen_inventory_v2.md`),
> art direction (bible §10), accessibility settings
> (`broodline_accessibility.md`), or the server (`broodline_solo_execution.md`).

**Two findings.** `broodline_combat_engine.md` §9 hands rendering to a document that did not exist — *"Rendering is the problem, and it is not this document's"* — and no document in the set has owned the client at all: not Addressables, not install size, not memory, not what the client may cache, not what works offline. This is that document.

And **the render budget needs a Phase 1 proof, not a Phase 3 discovery.** `broodline_combat_engine.md` §9 already says so and names the test case — §4.

---

## 1. The layering, and why it is enforced by assemblies

Six assemblies. The dependency arrows only point one way, and Unity assembly definitions make that a compile error rather than a convention — the same mechanism `broodline_solo_execution.md` §7.1 uses to keep `UnityEngine` out of the engine.

```
Broodline.Sim          the engine (UPM package)   — references nothing
      ▲
      │ read-only
Broodline.View         renders SimState           — references Sim
                                                     never mutates it
Broodline.Model        player + world state, cached from /v1/sync
      ▲                                             references Generated.Api
      │
Broodline.Net          HTTP, auth, the outbox     — references Model, Generated.Api
      ▲
      │
Broodline.UI           59 screens, shared components — references Model, Net
      ▲                                              **never references Sim**
      │
Broodline.Game         composition root, scene wiring — references all
```

**Two rules carry the weight:**

**`Broodline.UI` does not reference `Broodline.Sim`.** A screen that can reach into simulation state will eventually compute something from it, and that computation is a game rule living in the client. The only path from simulation to interface is `View`, which reads `ref readonly SimState` and writes nothing.

**`Broodline.View` renders; it never decides.** Damage numbers, breach diagnoses and outcomes come from the engine's own output. If the view needs a number that the engine does not expose, the engine gains an accessor — the view never derives it.

---

## 2. The simulation-to-render boundary

The engine runs at a fixed 30 Hz. Rendering does not.

`View` owns an accumulator, calls `Step()` exactly as many times as the elapsed time allows, and **interpolates between the last two `SimState` snapshots** for display. The engine never sees a variable delta and never learns that rendering exists.

Three consequences that are easy to get wrong:

- **Two snapshots must be retained**, previous and current, because interpolation needs both. The engine exposes a read-only view; `View` copies the minimum it needs for interpolation rather than deep-copying world state each tick.
- **Input is captured at a tick boundary.** A Rally tap records the tick index, not a timestamp — `broodline_combat_engine.md` §8 makes the replay store the tick, and the input log the server re-simulates from must be exactly what the local run consumed.
- **A dropped frame must never drop a tick.** If rendering stalls, the accumulator catches up by stepping the engine more times. A simulation that skips ticks to keep up produces a run the server will not reproduce, and the player loses the reward for a wave they actually won.

---

## 3. Frame and memory budgets

**Target: 60 fps.** At 30 Hz simulation and 60 Hz rendering, one simulation step lands every other frame, so the worst frame carries a full tick plus a full render inside **16.6 ms**.

The engine is not the problem. A hundred entities of integer arithmetic with one targeting pass is well under a millisecond. **Rendering owns essentially the whole budget**, which is why it needs a number and a proof rather than an assumption.

**Memory: 800 MB peak.** iOS terminates on memory pressure without a crash log worth reading, and the oldest supported device sets the ceiling. 800 MB leaves room under jetsam on a 4 GB device with a wave running, the UI resident and Addressables warm.

---

## 4. The entity-count proof — a Phase 1 gate

`broodline_combat_engine.md` §9 asks for this explicitly and it has not been scheduled.

**Wave 44 is the test case.** `broodline_waves_37_44.md` §7 and both later wave documents flag the same worry: wave 44 and five of chapter 8's waves put close to **a hundred entities** on the board — sixty Skirmishers, three Broods becoming thirty-nine, five creatures.

> **The proof: wave 44's entity count, rendered at 60 fps on the oldest supported device, with real creature meshes rather than capsules.**

It runs in Phase 1 alongside `broodline_rig_proof.md`, and for the same reason: **it can invalidate an assumption that everything downstream is built on.** If a hundred skinned meshes cannot hold 60 fps, the answer is a rendering strategy change — crowd impostors, baked vertex animation, fewer unique bodies on screen — and that changes the art budget. Discovering it in Phase 3 means rebuilding the renderer after the content is authored against it.

Two things `broodline_combat_engine.md` §9 already asks the engine for, which this depends on:

- **Functionally identical entities are instanced, not individually animated.** Sixty Skirmishers are one instanced draw with per-instance animation offsets, not sixty skinned mesh renderers.
- **The engine exposes a per-tick entity count**, so the render layer degrades *before* it drops frames rather than after.

**The degradation ladder, decided in advance rather than during the frame:**

| Entity count | |
|---|---|
| Under 40 | Full fidelity — individual skinned meshes, full VFX |
| 40–70 | Identical raider types collapse to instanced rendering; hit VFX pooled and capped |
| Over 70 | Animation update rate halves for non-foreground entities; death VFX becomes a single shared effect |

The ladder keys off the engine's entity count, which is deterministic — so **two players watching the same replay see the same fidelity**, and a device that degrades never diverges from one that does not. Degradation touches rendering only; it can never touch `SimState`.

---

## 5. Runtime creature assembly

`broodline_rig_proof.md` owns the socket standard and proves that twelve trait parts mount on two dissimilar bodies in either socket. This owns what happens at runtime.

A creature is **assembled, not authored**: a species body plus up to two trait attachments bound to named sockets, plus an Instinct marker. Six species × twelve traits × two slots is far too many combinations to author, which is the entire reason bible §10.3 requires socket standardisation.

**Assembled creatures are cached by `(species, traitA, traitB)`.** The roster shows dozens of creature cards; re-binding attachments per card per frame is the obvious way to lose the budget in §3.

**Attachments are individually addressable**, so a new trait part ships as content rather than as an app update — which is what makes `broodline_live_ops_events.md`'s cadence possible without App Review in the loop.

**The creature card is a 2D render of the same assembly**, not a separate art path. `broodline_screen_inventory_v2.md` §11 calls it "the atom of the interface" and puts it on at least nine screens; a second, divergent representation of a creature is how the roster and the battlefield end up disagreeing about what a player owns.

---

## 6. Content packaging and install size

**Install size is a conversion metric.** It is measured in installs lost, not megabytes, and it is the one client decision that shows up directly in acquisition cost.

> **Target: under 150 MB initial download.**

Everything past the first hour streams.

| In the binary | Streamed via Addressables |
|---|---|
| The first hour, per `broodline_build_order.md` Phase 2 | Remaining regions and terrain families |
| Two base-stock species, fully rigged | The other four species |
| Waves 1–6 and their raiders | Chapters 2–8 |
| One region's environment | Event and seasonal content |
| Shared UI components, icon atlas, trait pips | Cosmetics |
| Launch-locale strings and fonts | Additional locales and CJK font substitutes |

Two rules that keep this honest:

- **Content is fetched ahead of need, never at the moment of need.** A player who taps a region must not wait on a download. Prefetch on the map screen, on the wave-select screen, and on idle.
- **A failed fetch degrades, never blocks.** If chapter 4's assets have not arrived, the player is told and the campaign waits — the app does not sit on a spinner. This is also the FTUE-critical case: the first hour must never depend on the network for content, because it is in the binary precisely so it does not.

`broodline_localization.md` §8's CJK substitutes are streamed per locale rather than shipped, which is what keeps the launch binary from carrying fonts for languages that do not exist yet.

---

## 7. Local persistence

The client is a cache with an outbox. It is never a source of truth.

**What the client stores:**

| | |
|---|---|
| Config bundle | Versioned and immutable — §5.2 of `broodline_solo_execution.md`. Keyed by version, so a new bundle is a new file rather than a mutation |
| Addressable content | Managed by Unity's cache, with a size ceiling and eviction |
| Last `/v1/sync` snapshot | So a cold start renders instantly, then revalidates |
| The outbox | Pending mutations, durable across a kill — §8 |
| Preferences and accessibility settings | Per `broodline_accessibility.md`'s settings list. Local only; they follow the device, not the account |
| Auth refresh token | iOS Keychain, never in a file |

**What the client never treats as truth**, per `broodline_data_model.md` §8: balances, splice outcomes, cooldowns, node yields, immunity state, matchmaking bands. These are cached **for display** and are overwritten by the next sync without a merge. A cached balance is a label, not a number the client may do arithmetic on before spending.

**The cold-start sequence:** render the cached snapshot immediately, call `/v1/sync`, replace. A player never watches a spinner to see their own roster. If the config bundle version in the sync response differs, fetch it before rendering anything that depends on it — a stale trait table renders wrong pips, and the pip is the most-repeated element in the app.

---

## 8. The offline model and the outbox

Mobile networks are always flaky, and `broodline_solo_execution.md` §6.3 already makes every mutation idempotent. That is what the client's offline behaviour is built on.

**Playable offline:** campaign waves — the engine is local, so the wave simulates and the submission queues. Also the roster, the Codex, lineage view, and anything else that reads cached state.

**Not available offline**, because they are server-authoritative and cannot be faked convincingly: splice (the server rolls), harvest claim, raids, alliance actions, the store. These show as unavailable rather than failing on submit.

**The outbox:**

- Each entry carries **an idempotency key generated when the action is taken**, not when it is sent. A retry after a kill, a crash or three days offline sends the identical key and cannot double-grant.
- **Ordered per player**, drained oldest-first. A splice that depends on a wave reward must not overtake it.
- **Flushed on foreground and on connectivity**, with exponential backoff.
- **The server's response is truth.** A queued campaign submission that fails validation loses its reward — §9.3 of `broodline_solo_execution.md` grants optimistically and reconciles, so the client must be able to show a reward being withdrawn without pretending it never appeared.
- **Entries expire.** An outbox entry older than the server's idempotency window (24 hours) can no longer be safely replayed; it is dropped with a mail explaining what did not happen, rather than replayed into a double grant.

---

## 9. Scenes, navigation and shared components

**One persistent root scene, everything else additive.** The root holds the composition root, the network layer, the tab bar and the persistent top bar — `broodline_screen_inventory_v2.md` §2 makes both persistent, with Splice Charges and their regen timer always visible. Wave Defense loads additively and unloads on exit; it is the only scene with a 3D battlefield and the only one with a per-frame budget worth defending.

**Five tabs — Map · Ark · Splice · Lab · Allies.** No Store tab. Onboarding starts with two and reveals the rest as systems unlock, so the tab bar is data-driven from progression state rather than a fixed prefab.

**Bottom sheets are an overlay layer, not screens.** The Codex sheet is opened from every trait pip in the app; making it a navigation destination would put it in the back stack of every screen. It overlays, it dismisses, it does not push.

**Pushed sub-screens hide the bottom nav and carry a back chevron.**

**The nine shared components in `broodline_screen_inventory_v2.md` §11 are prefabs with explicit APIs, built before the screens that use them.** The creature card and the trait pip are the two that matter most — the pip is the most-repeated element in the app and the card appears on at least nine screens. `broodline_build_order.md` Phase 1 already puts the Codex bottom sheet first for the same reason: it is a dependency of nearly every screen rather than a screen of its own.

**The probability table is regulatory-relevant.** Splice and Roulette both display published odds, and `broodline_data_model.md` §8 makes the roll server-side precisely so those odds are demonstrable. The component renders numbers from config; it never computes them.

---

## 10. Build configuration

| | |
|---|---|
| Scripting backend | IL2CPP, ARM64 |
| Graphics | Metal |
| Orientation | Portrait-locked |
| Binary | Universal iPhone and iPad |
| Managed stripping | Enabled, with an explicit `link.xml` |

**The `link.xml` is not optional.** Managed stripping removes types that appear unused, and generated API clients deserialize into types nothing statically references. Stripping them produces a runtime failure that appears only in release builds on device — the worst place to find it. Preserve `Broodline.Api.Client` and `Broodline.Model` explicitly.

**Determinism note:** the engine's determinism guarantees hold under IL2CPP because it contains no floating point and no unordered iteration — `broodline_solo_execution.md` §7.1. The nightly CI diff compares CoreCLR against a macOS ARM64 IL2CPP build for exactly this reason, and a physical device run before each release closes the remaining gap.

---

## 11. Guardrails

- `Broodline.UI` never references `Broodline.Sim`
- `Broodline.View` renders simulation output and never derives it
- A dropped frame never drops a simulation tick
- Rendering degradation keys off the engine's deterministic entity count and never touches `SimState`
- Input is recorded at a tick index, never a timestamp
- The client is a cache with an outbox and is never a source of truth
- Server-authoritative values are cached for display only and are never operated on
- Idempotency keys are generated when the action is taken, not when it is sent
- The first hour ships in the binary and never depends on the network
- A failed content fetch degrades and explains; it never blocks on a spinner
- Creature assemblies are cached by `(species, traitA, traitB)`
- The probability table renders published odds and never computes them

---

## 12. Open questions

1. **What is the oldest supported device?** Everything in §3 and §4 depends on it, and it is a business decision rather than a technical one. A 2020-era A14 device is the natural floor — old enough to cover a long tail, new enough that 60 fps with a hundred entities is plausible. It must be fixed before the entity-count proof runs, because the proof is meaningless without a target.
2. **60 fps or 30?** §3 assumes 60. A portrait tower defence at 30 fps is defensible and roughly doubles the render budget, at a real cost to how the game feels in the hand. The entity-count proof should measure both and let the answer be evidence.
3. **Does the 150 MB target survive two rigged species?** `broodline_build_order.md` budgets thirty-six character assets. Two fully rigged species plus a region plus UI may not fit under 150 MB, in which case either the target moves or the first hour ships with one species and streams the second.
4. **iPad orientation and multitasking.** A portrait-locked universal binary is the cheapest correct answer for a portrait-designed game, but App Review's expectations for iPad builds have moved between guideline revisions. Verify against current guidance before committing the layout work rather than inheriting last year's answer.
5. **Where does the replay viewer live?** `broodline_screen_inventory_v2.md` makes it launch-critical and it is `Run()` plus the Wave Defense renderer — but §9.4 of `broodline_solo_execution.md` means a superseded replay renders a result card instead. Whether that is the same screen in two states or two screens is a UX question with a real architectural consequence.
6. **Is the tab bar's progressive reveal driven by server state or local progression?** Onboarding starts with two tabs. If the unlock state is local, a reinstall mid-onboarding shows the wrong bar; if it is server-side, it belongs in `/v1/sync`. The second is probably right and costs a field.

---

*Owns: client layering and assembly boundaries, the simulation-to-render contract, frame and memory budgets, the degradation ladder, runtime creature assembly, content packaging and install size, local persistence, the offline model and outbox, scene and navigation structure, and build configuration. Does not own: the simulation (`broodline_combat_engine.md`), the socket standard (`broodline_rig_proof.md`), screen contents and copy (`broodline_screen_inventory_v2.md`), art direction (bible §10), accessibility settings (`broodline_accessibility.md`), or anything server-side (`broodline_solo_execution.md`).*
