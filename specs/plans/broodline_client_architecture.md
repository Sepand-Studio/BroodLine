---
status: current
folder: 03-technical
note: >
  Client layering, the simulation-to-render contract, frame and memory
  budgets, runtime creature assembly, content packaging, local
  persistence, the offline outbox and build configuration.
---

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

**Reference device: A13 with 3 GB — iPhone 11, iPhone SE (2020), iPad 9th gen.**

The floor is set by the cheap devices, not the old flagships. The SE line and the base iPad are exactly the price-sensitive hardware a free-to-play audience runs, and excluding them is a revenue decision rather than a technical one. The consequential difference between the A13 and A14 bands is **memory, not GPU** — the SE (2020) and iPad 9 carry 3 GB where every A14 device carries 4.

**Frame rate: 60 target, 30 fallback.** At 30 Hz simulation and 60 Hz rendering, one simulation step lands every other frame, so the worst frame carries a full tick plus a full render inside **16.6 ms**. A locked 30 fps is the last rung of the degradation ladder (§4) and a battery option in settings, which doubles the budget to 33.3 ms when it engages.

**Interpolation stays regardless** — §2. Rendering 1:1 with ticks at a locked 30 fps would delete it, but a 60 fps target with a 30 fps fallback needs it, and capping 60 down is always possible where adding it back is not.

The engine is not the problem. A hundred entities of integer arithmetic with one targeting pass is well under a millisecond. **Rendering owns essentially the whole budget**, which is why it needs a number and a proof rather than an assumption.

**Memory: 600 MB peak.** iOS terminates on memory pressure without a crash log worth reading, and the reference device sets the ceiling. 600 MB leaves room under jetsam on a **3 GB** device with a wave running, the UI resident and Addressables warm. This is the single tightest constraint in the client and it is a direct consequence of the device floor.

---

## 4. The entity-count proof — a Phase 0 gate

`broodline_combat_engine.md` §9 asks for this explicitly and it has not been scheduled.

**Wave 44 is the test case.** `broodline_waves_37_44.md` §7 and both later wave documents flag the same worry: wave 44 and five of chapter 8's waves put close to **a hundred entities** on the board — sixty Skirmishers, three Broods becoming thirty-nine, five creatures.

> **The proof: wave 44's entity count, rendered on an A13 / 3 GB device with real creature meshes rather than capsules, measured at both 60 and 30 fps and against the 600 MB ceiling.**

It runs alongside `broodline_rig_proof.md` in Phase 1 of `broodline_build_order.md` — Phase 0 of `broodline_solo_execution.md` §8.2 — and for the same reason: **it can invalidate an assumption that everything downstream is built on.** If a hundred skinned meshes cannot hold 60 fps, the answer is a rendering strategy change — crowd impostors, baked vertex animation, fewer unique bodies on screen — and that changes the art budget. Discovering it in Phase 3 means rebuilding the renderer after the content is authored against it.

Two things `broodline_combat_engine.md` §9 already asks the engine for, which this depends on:

- **Functionally identical entities are instanced, not individually animated.** Sixty Skirmishers are one instanced draw with per-instance animation offsets, not sixty skinned mesh renderers.
- **The engine exposes a per-tick entity count**, so the render layer degrades *before* it drops frames rather than after.

**The degradation ladder, decided in advance rather than during the frame:**

| Entity count | |
|---|---|
| Under 40 | Full fidelity — individual skinned meshes, full VFX |
| 40–70 | Identical raider types collapse to instanced rendering; hit VFX pooled and capped |
| Over 70 | Animation update rate halves for non-foreground entities; death VFX becomes a single shared effect |
| Still short | **Drop to 30 fps for the remainder of the wave**, never mid-wave more than once |

The frame-rate rung is last because it is the most visible, and it latches for the wave rather than oscillating — a frame rate that hunts between 60 and 30 reads as worse than either.

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

**The first hour needs four species, not two.** `broodline_base_stock.md` §5.1 hands the player a **Vetch** and an **Ember** at wave 1 as the tutorial splice parents, consumed at beat 6 to produce Cinderplate; Founder 1 is a **Hollow**, arriving session one at beat 3 as the creature the player names; and §5.2 grants a **Pale** from the Wave Defeat screen at wave 6, which `broodline_build_order.md` Phase 2 places inside the first hour. Skitter arrives day 2 and Loam day 3, so all six are in hand within 72 hours.

| In the binary | Streamed via Addressables |
|---|---|
| The first hour, per `broodline_build_order.md` Phase 2 | Remaining regions and terrain families |
| **Vetch, Ember, Hollow and Pale**, fully rigged | **Skitter and Loam** — needed at day 2 and day 3, so prefetched from first launch against a window measured in days |
| Waves 1–6 and their raiders | Chapters 2–8 |
| One region's environment | Event and seasonal content |
| Shared UI components, icon atlas, trait pips | Cosmetics |
| Launch-locale strings and fonts | Additional locales and CJK font substitutes |

**Rough budget:** the Unity runtime with URP under IL2CPP is 35–50 MB before any content and is the largest single item; four rigged species ~16 MB, twelve trait attachments ~4 MB, early raiders ~8 MB, one region ~25 MB, UI atlases ~15 MB, audio ~12 MB, Latin fonts ~2 MB. **Roughly 117–132 MB**, so 150 MB survives with less headroom than it appears — and the two dominant variables, the engine binary and the region environment, are not things that can be trimmed late.

The cellular download ceiling is no longer the constraint; iOS lets users download over cellular at any size. 150 MB is a conversion target, not a compliance one.

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

**Layout is size- and aspect-tolerant by construction** — safe-area driven, no fixed pixel positions, and a declared minimum window size. §10 explains why this is now a requirement rather than good practice. It is cheap as a rule from the first screen and expensive as a retrofit across fifty-nine.

**The tab bar's progressive reveal stores nothing.** Onboarding starts with two tabs and reveals the rest as systems unlock, but unlock state is not persisted anywhere: the bar is a **pure function of campaign progress and Geneticist Tier**, both already returned by `/v1/sync`. There is no local state to lose on reinstall and no new server field.

**The gate thresholds live in the config bundle**, not in code — which wave reveals the Lab, which tier reveals Allies. `broodline_build_order.md` §5 makes FTUE the thing playtest has to answer, so retuning the reveal must be a bundle publish rather than an App Review cycle.

Deriving this on the client is safe because it is an **affordance, not a capability**. The server refuses an alliance join below the gate regardless of what the tab bar shows. The client decides what to display; the server decides what to allow. A fresh install with no cached snapshot shows the minimum two tabs until the first sync returns — correct for a new player, and wrong for a reinstalling veteran only for the length of one network call.

### 9.1 The replay viewer

`broodline_screen_inventory_v2.md` makes it launch-critical, and §9.4 of `broodline_solo_execution.md` means a replay recorded under a superseded engine version cannot be played back at all. That does not need two screens.

> **One screen. The outcome and breach diagnosis are always primary; playback is an action on it.**

Entering a replay always shows the same thing — result, integrity remaining, and the three-boolean breach diagnosis from `broodline_combat_engine.md` §7. That diagnosis *is* the actionable content: it names whether the trait was absent, present at insufficient coverage, or misplaced, which is what `broodline_collectors_raiding.md` means when it calls the replay the thing that makes losing survivable. It is stored, so it survives an engine change.

A **Watch** button appears when the engine version matches, loading Wave Defense additively with input disabled. A superseded replay loses the button and gains a one-line notice.

Three things this buys: the raid mail list is browsable without loading a 3D scene per entry, §9.4's degradation becomes an absent button rather than a special case, and Wave Defense gains one flag — input enabled or not — rather than a second renderer.

**Scrubbing is nearly free, which is unusual.** Re-simulating from tick 0 to any point in a 90-second wave costs roughly 20 ms, so seeking backward is a full re-run nobody notices. Most replay systems need keyframes to do this; determinism gives it away.

**The nine shared components in `broodline_screen_inventory_v2.md` §11 are prefabs with explicit APIs, built before the screens that use them.** The creature card and the trait pip are the two that matter most — the pip is the most-repeated element in the app and the card appears on at least nine screens. `broodline_build_order.md` Phase 1 already puts the Codex bottom sheet first for the same reason: it is a dependency of nearly every screen rather than a screen of its own.

**The probability table is regulatory-relevant.** Splice and Roulette both display published odds, and `broodline_data_model.md` §8 makes the roll server-side precisely so those odds are demonstrable. The component renders numbers from config; it never computes them.

---

## 10. Build configuration

| | |
|---|---|
| Scripting backend | IL2CPP, ARM64 |
| Graphics | Metal |
| Orientation | Portrait only — see below |
| Binary | Universal iPhone and iPad |
| Managed stripping | Enabled, with an explicit `link.xml` |

**Portrait-only no longer means fixed-size.** `UIRequiresFullScreen` is deprecated, and under the iOS 27 SDK declaring portrait alone in `UISupportedInterfaceOrientations` does not opt the app out of resizing — it makes the app **non-continuously resizable**, so the window snaps between discrete states instead of reflowing while dragged. Apple's framing is that this "enables discrete resizing that respects your supported interface orientations so games always render at full quality," which reads as a deliberate carve-out for this exact case.

**Do not take the continuous-resizability workaround.** It requires declaring all four orientations in `Info.plist` plus per-view-controller overrides, and widening the plist sets an app-wide ceiling that also **enables upside-down rotation on iPhone** — which Apple recommends against and which a portrait-designed game does not want.

**What this actually requires**, and it is a §9 rule rather than a build setting: the UI must tolerate being resized. Safe-area-driven layout, no fixed pixel positions, a minimum window size declared through `UIWindowScene.sizeRestriction`, and a combat camera framed by a design-safe region rather than a fixed aspect ratio.

**Verified September 2026, against beta-period behaviour with acknowledged gaps between Apple's stated intent and shipping behaviour. Re-verify before the iPad layout work rather than trusting this paragraph.**

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
- Layout is safe-area driven with no fixed pixel positions, and declares a minimum window size
- The frame-rate degradation rung latches for the wave and never oscillates
- The first hour ships four species; nothing in it waits on a download
- Tab reveal is derived from progression and stored nowhere

---

## 12. Decisions and what remains open

**Resolved:**

| | |
|---|---|
| **Reference device** | A13 / 3 GB — iPhone 11, iPhone SE (2020), iPad 9th gen. The floor is set by the cheap devices, and memory is the binding constraint |
| **Frame rate** | 60 target with a locked-30 fallback as the last degradation rung and a battery setting. Interpolation stays |
| **Memory ceiling** | 600 MB peak, a direct consequence of the 3 GB floor |
| **Binary contents** | Four species — Vetch, Ember, Hollow, Pale. Skitter and Loam prefetch against a 24–48 hour window |
| **Install target** | 150 MB, estimated at 117–132 MB. The engine binary and the region environment carry the risk |
| **iPad** | Portrait only, discrete resizing, size-tolerant layout. No all-four-orientations workaround |
| **Replay viewer** | One screen; diagnosis primary, playback an action |
| **Tab reveal** | Derived from progression, thresholds in config, nothing stored |

**Still open:**

1. **Does Unity 6 expose what iPadOS 27 now needs?** §10's requirements assume access to the scene-based lifecycle and `UIWindowScene.sizeRestriction`. Whether Unity 6's iOS player surfaces those cleanly, or needs a native plugin, is unverified — and there is active discussion about iPadOS 27 behaviour with Unity games specifically. **This should be checked during the Phase 0 entity-count proof**, since that proof already puts a real build on a real device.
2. **Does 150 MB survive real art?** §6's budget is an estimate against stylised mobile assets. The first honest measurement comes from the rig proof, which produces two finished species — enough to extrapolate the other two.
3. **Re-verify the iPad rules before the layout work.** §10 is dated and describes beta-period behaviour.

---

*Owns: client layering and assembly boundaries, the simulation-to-render contract, frame and memory budgets, the degradation ladder, runtime creature assembly, content packaging and install size, local persistence, the offline model and outbox, scene and navigation structure, and build configuration. Does not own: the simulation (`broodline_combat_engine.md`), the socket standard (`broodline_rig_proof.md`), screen contents and copy (`broodline_screen_inventory_v2.md`), art direction (bible §10), accessibility settings (`broodline_accessibility.md`), or anything server-side (`broodline_solo_execution.md`).*
