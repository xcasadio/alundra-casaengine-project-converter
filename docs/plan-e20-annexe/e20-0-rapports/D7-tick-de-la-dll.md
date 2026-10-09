# D7: the DLL tick, with a draft E20.a contract

> Report returned by one agent of the E20.0 workflow of 2026-10-09 (docs/plan-e20-heros.md section 1.2), kept verbatim as evidence.
> Where it disagrees with its check or with the critic, the plan section 0.2 holds the reconciled fact.

# D7 report: inventory of the DLL tick (read-only)

Labels: F = FACT (file:line, or binary address with instruction words), I = INFERENCE (basis given), U = UNKNOWN. DLL paths are under `Alundra/Scripts/`.

## 1. Order of one rendered frame

**Engine (F, `CasaEngineMonogame/CasaEngine/Framework/Scene/World/World.cs:517-585`).**
1. `RuntimeSystems.Update` (`:531`) runs the controllers once per rendered frame.
2. Each entity's `Update` runs in `_entities` order (`:533-551`). That includes `AlundraEntityScriptProxy.Update`.
3. `AlundraWorldProxy.Update` runs last (`:581-584`).
4. Hero first in `_entities`: I, not read. The binary has the hero in slot 0.

**Per entity (F, `AlundraEntityScriptProxy.cs`).**
- **Hero pad sample (`:1207-1214`):** `LastPadState = BuildPadState()` once per frame, ungated.
- **Gate (`:1193`):** read once per frame from the flags left by the previous frame.
- **Root pull, once per frame (`:1257-1286`).**
- **NPC (`:1312-1383`):** for tick in 0..n-1: `PickEventTrigger`, `RunPickedEvent`, `StepAnimationClock`, `TickScriptedNpc`, `EvaluateEntitySupport`. All n ticks of one entity run before the next entity starts.
- **Hero (`:1398-1456`):**
  - `MovePlayer` runs ONCE per frame, even with 0 ticks (`:1408`).
  - Then n times: `StepAnimationClock`, `Tick(1)`, `UpdateVramFlags` (`:1415-1426`).
  - Then once per frame: `UpdateGroundSlope`, `UpdateVramFlags`, `UpdateFloorHeight` (`:1437-1455`).
- **After the ticks:** `SyncAnimation` once per frame (`:1464`), then `SyncTransform` (`:1221`).

**World proxy (F, `AlundraWorldProxy.cs:2144-2548`).** For tick k:
1. `TickPad.Update(LastPadState.ButtonsHold)` (`:2243`).
2. Dialogue box pass on the Square of tick k-1, then the text, choice and save screens (`:2251-2270`).
3. If the hero exists and the gate is open: `ClearHoldFlagsOfPendingSwitches` when k>0 (`:2277`), `RunMapEventsPass` (`:2282`), `RecycleDestroyedEntities` (`:2286`).
4. Gate read again, then the inventory block (`:2290-2325`).

After the loop:
- Lists and riding refresh (`:2353`, `RefreshUpdateProxiesAndCollidables` at `:2918-2930`).
- `RunPendingEventTriggers` once per tick (`:2364-2373`).
- Effects, camera, backdrops, fade, warp, HUD.
- Freeze and `CloseFrame` (`:2514`).

**Frames of 0, 1 and 2+ ticks (max 4, `AlundraScriptedMotion.cs:57`).**

| Ticks | What runs |
|---|---|
| 0 | Pull, hero `MovePlayer` (once), `SyncAnimation`. No NPC script, no motion, no `TickPad` step, no map events, no dialogue pass. |
| 1 | All entities in engine order: scripts and motion. Then pad, box and map events. |
| 2+ | Entity-major: E0 t0, E0 t1, E1 t0, E1 t1, … Then world tick-major: pad0, box0, M0, inv0, pad1, box1, M1, inv1. The hero's `MovePlayer` still runs once. |

**The first frame** is forced to at least 1 tick (`_firstFrameStillOpen`, `AlundraWorldProxy.cs:640-648`, cleared at `:2548`).

**Binary (F).** `UpdateWorld` calls these in this order:
- `jal 0x8003C67C` at `0x8002E100` (word `0x0c00f19f`): map events.
- `jal 0x8003B388` at `0x8002E108` (`0x0c00ece2`): entities.
- `jal 0x8003C410` at `0x8002E110` (`0x0c00f104`): not identified; the DLL treats the following pass as effects.

Inside `0x8003B388`:
- `andi 0x48` at `0x8003B394` (`0x30420048`), `bnez` at `0x8003B398`. If the mask is non-zero, ONLY `0x800384F4` (lists) runs, at `0x8003B408`.
- Otherwise the calls are `0x80038634`, `0x800386D0`, `0x80038998`, `0x800384F4`, `0x80038E18`, `0x80038364`, `0x80038E84`, `0x80039300`, at `0x8003B3A0-0x8003B3F0`.
- `RunMapEvents` has the same gate (`andi 0x48` at `0x8003C69C`, `bnez` at `0x8003C6A0`).
- `0x800386D0` calls `MovePlayer` unconditionally before its loop (`jal 0x80031B50` at `0x800386EC`, word `0x0c00c6d4`). Entities start at slot 1 (`addiu $s2,$zero,1` at `0x800386F4`).

**Counters pass `0x80038998` (F, loop `0x800389D0-0x80038A18`).** It covers slots 0 through `[0x80134360]` inclusive, hero included, with no status filter. Per slot it does:
- `+0x1C` incremented.
- `+0x1CC` decremented if non-zero.
- `+0x1D0` decremented if non-zero.
- Zero stored at `+0x224` (TouchingEntity), `+0x12C` (RidingEntity) and `+0x220` (HitCounter).

## 2. What assumes `MovePlayer` per rendered frame

1. **Take-off stamp** (F). `JumpStartTickStamp` is declared at `AlundraEntityScriptProxy.cs:284`. The guard sits at `AlundraPlayerManager.cs:294-301` and the stamp is taken at `:443`. `MotionTickCount` is incremented at `AlundraScriptedMotion.cs:218`. Under per-tick `MovePlayer` the guard and the stamp are dead.
2. **`LastPadState` readers** (F).
   - Hero `MovePlayer` (`AlundraEntityScriptProxy.cs:1407`).
   - Opcode `0x2F` modes 0 and 1 (`AlundraEventProgramRunner.cs:1738-1764`). Modes 2 and default are logged as degraded, though `TickPad` already has `ButtonsReleased` and `ByInterval`. Map events read the per-frame engine edge once per tick: twice in a 2-tick frame, never in a 0-tick frame.
   - `TickPad` takes only the hold word (`AlundraWorldProxy.cs:2243`). That is correct.
   - `AlundraDialogueDirector.cs:565-569` `Tick()` reads `LastPadState`. I found no direct production caller. U for a call through an interface.
3. **Square edge.** `CheckEntityInteraction` reads `pad.ButtonsJustPressed & Square` (F, `AlundraPlayerManager.cs:718`). That is the engine's per-frame edge. The dialogue uses `TickPad` Square (`AlundraWorldProxy.cs:2244`).
4. **Per-frame probes.** `Slope_18c` (`UpdateGroundSlope`) and `FloorHeight` are refreshed once per frame, after the ticks (F, `:1437`, `:1455`). Only `VramFlags` is refreshed per tick (`:1424`). A per-tick `MovePlayer` reads `Slope_18c` for the ladder gate (`AlundraPlayerManager.cs:248`) and `FloorHeight` for the descent (`:359`). Tick 2 of a frame would see the previous frame's values. E20.a must move both probes into the tick.
5. **`IsOnGround`** is pulled once per frame (`:1269-1272`), except in the air state, where the tick owns it (`AlundraScriptedMotion.cs:~230`). Tick 2 of a frame on the ground reads the frame-head value. I, low risk.
6. **Gate** is read once per entity per frame (`:1193`), not per tick as in the binary (`0x8003B398`).
7. **`RidingEntity`** is recomputed per frame (`AlundraWorldProxy.cs:2930`). A counters pass that clears it each tick would erase a value recomputed only per frame. Leave `RidingEntity` out of the DLL counters pass until riding is recomputed in the tick. I.
8. **First frames, O-E19-80.**
   - The `LoadingMap` case has the terrain guard (`AlundraPlayerManager.cs:265-271`, `:463`).
   - The common tail reads `IsHeroInTheAir` (`:428`, `:453`) without that guard.
   - A fresh pawn on a `0x52`/`0x53` arrival with Idle/Moving can read the engine's `IsGrounded` as false (I, consistent with the O-E19-80 text at `docs/plan-e19-opcodes.md:12384`).
   - Once map events run before `MovePlayer`, the intro-lock mechanism of E19.v2 is gone. The no-lock arrival remains. It needs the real-game probe first (E19.v2 method), then probably the same floor rule (`PosZ > floor`) applied in `IsHeroInTheAir`. I, to be measured.

## 3. Tests that pin a per-frame effect

Rigs with `freePad:false` have no `PlayerController`, so `MovePlayer` is skipped. I, from `AlundraEntityScriptProxy.cs:1400` and `AlundraJumpTestSupport.cs:291`. They are unaffected.

- **`AlundraHeroFallAndPadJumpTests.cs:176-190` (SJ6b)**
  - It asserts `ForceX == 159744` after one 2-tick frame, with the comment that the binary gives 155136 at the second tick (`:183`).
  - UH2 pins tick 2 at 155136 (`:209`).
  - With a per-tick `MovePlayer`, the expected value is 155136 (animation 2 then `0x2C`). Source: the test comment and UH2. I did not re-derive it from the oracle.
- **`:168-173` (SJ6).** `PosZ == 622592` should stay. I.
- **`:141-151` (SJ5b)**
  - A tickless frame no longer runs `MovePlayer`, so `Target == 43` at `:145` becomes Idle (0).
  - The test must hold Cross until the first tick, because a press that starts and ends between two ticks has no edge on `TickPad`.
- **`:153-165` (SJ5c, 144 Hz).** Same issue: Cross is held on frame 1 only (`:160`), and the first tick is frame 3. Cross must stay held until the tick.
- **`AlundraHeroJumpStatesTests.cs:239-272`.** SJ4 and SJ4b test the stamp guard and are deleted or replaced. `:222-233` (SJ3b) sets `JumpStartTickStamp = -1` and keeps its meaning with the field gone.
- **`AlundraHeroJumpStatesTests.cs:216-219`.** "Triangle alone gives Idle" is unchanged by E20.a. It inverts in E20.d.
- **`AlundraHeroFallAndPadJumpTests.cs:283-320` (SJ8a/b).** The edge is spent while a menu is open. This stays true because `TickPad` is advanced ungated (`AlundraWorldProxy.cs:2243`).
- **`AlundraHeroFallAndPadJumpTests.cs:570-583`.** 30 frames of 0.04 s going down a ramp, `Target == 1` every frame. Re-check it.
- **`AlundraHeroWaterIceTests.cs:160-173` (UW6).** Pins the per-tick flag refresh. Add the same case for slope and floor.
- **`AlundraInteractionPassTests.cs:395-409`.** It pins a one-shot assignment in 0-tick and 3-tick frames. It is valid under any model.
- **Hero traces.** `HeroTraceHarnessTests.cs:481` calls `world.Update(dt)` to write the four `docs/hero-trace-389-*.txt` files (freestep and fixedstep). They are re-measured.
- **`IntroTraceHarnessTests.cs:833-879`.** It has its own `RunFrame` that copies the production order: entities, then map events per tick, then pending triggers per tick. It must follow any reorder, or share the new code.
- **Inversion pins**
  - `AlundraDialogueBoxOrderTests.cs:58` (K1-entity), `:241` (K4) and `:406` (K7) pin the "entity" row of the ADR-0029 table (first pass at N, release at T+19).
  - `AlundraAnimationSoundTests.cs:281` pins the hero's tick one frame after a `0x5B`.
  - `docs/plan-e19-opcodes.md:4691-4705` holds the table.

**Blast radius.** The test project has 433 `.Update(` calls in 72 files, and 41 files use fake `IAlundraScriptHost` hosts. Moving the per-entity tick bodies out of `AlundraEntityScriptProxy.Update` breaks most of them.

## 4. Placement proposal (mapped to the binary order of plan 0.2.1)

**Requirement.** The counters pass and the hit pass must see "all entities' events of tick k" before and after. The plan's own test, a hit written at tick N, read at N+1, then cleared, with 2 ticks per frame, needs tick-major order.

| Binary pass | DLL placement per tick k |
|---|---|
| `0x8003C67C` map events, `0x80038634` recycle | unchanged (`:2282`, `:2286`), unless the inversion decision moves them (section 5) |
| `MovePlayer` (first in `0x800386D0`) | start of the entity block, hero only, on `AlundraPadState` built from `TickPad` for tick k. Add `ButtonsReleased` to the pad. |
| pick and run (`0x800386D0` loop) | same block; `RunPendingEventTriggers` joins it (the binary's re-scan is inside this pass) |
| `0x80038998` counters | new world-wide pass, all slots including the hero. Port `+0x1C`, `+0x1CC`, `+0x1D0`, `TouchingEntity` and `HitCounter`. Not `RidingEntity` (see 2.7). |
| `0x800384F4` lists | per tick, also when the gate is set (binary `0x8003B408`) |
| `0x80038E18` animation | `StepAnimationClock` per tick (already). `SyncAnimation` stays per frame (D-E19-13 kept). |
| `0x80038364` physics | hero `Tick` and NPC motion and support, plus `UpdateGroundSlope`, `UpdateFloorHeight` and `UpdateVramFlags` per tick |
| `0x80039300` hits | empty hook after physics until E20.c1 |

**Structure option A (recommended): a shared per-entity tick body plus a tick-major driver in `AlundraWorldProxy.Update`.**
- `AlundraEntityScriptProxy.Update` keeps pad sampling, root pull, `SyncAnimation` and `SyncTransform`.
- A flag "ticks driven by the world" makes `Update` skip its own tick loop in production.
- Without the flag (fake hosts, bare proxies), `Update` self-drives as today, using the same shared body. Rigs and the 433 calls keep working. The hero advances `TickPad` itself in that mode.
- Cost: two loop shapes over one body, and ordering tests written against the world proxy.
- Because the order of the entity block and map events is then one line in the driver, the inversion decision costs no structure.

**Structure option B (cheap, documented deviation): keep entity-major.**
- `MovePlayer` goes into the hero's tick loop, reading the pad words of `TickPad`.
- The counters pass and the hit hook run in the world proxy after all entity ticks of the frame.
- Deviation: in a 2+ tick frame the hit of tick 1 is not seen by the events of tick 2. It is seen in the next frame.
- This fails the plan's own 2-tick acceptance for E20.c1.
- Under B, `TickPad` must be advanced by the hero before the world loop (the hero's `Update` runs first), with per-tick words recorded for the inventory readers, which currently read the live pad inside the loop (`AlundraInventoryDirector.cs:414`, `:641`; `AlundraSubInventoryDirector.cs:502`).

**Option C (U).** The harness mentions a "future M3.a" with a 1/50 s fixed time step (`HeroTraceHarnessTests.cs:594`). With a fixed step each `World.Update` would be one tick and entity-major would equal tick-major. I did not check its status, and it is an engine and project decision.

## 5. Entities-before-map-events inversion

Cause: the engine updates entities before the world proxy (`World.cs:533-584`). The author already accepted it for the text box (D-E19-64, `docs/plan-e19-opcodes.md:221`).

**Fixing it in E20.a (needs option A).**
- Changes:
  - A map-event write (lock, `0x5B`, `0x64`) is seen by `MovePlayer` and entity scripts in the same tick, as in the binary.
  - Entity writes reach map events one tick later than now.
  - The ADR-0029 entity row becomes the map-event row (N+1, T+18), so the pins at `AlundraDialogueBoxOrderTests.cs:58`, `:241` and `:406` and `AlundraAnimationSoundTests.cs:281` change.
  - The first `MovePlayer` of a map runs after the intro lock, which removes the V2 mechanism.
  - The `ClearHoldFlagsOfPendingSwitches` hack for k>0 (`:2277`) stays, since `SyncAnimation` remains per frame.
- Cost:
  - Re-measure the intro points and the story arcs. Plan §4 stops on a moved pinned point unless the tranche explains it. I cannot size this read-only; the first task below measures it.
  - The intro harness `RunFrame` must be updated or shared.
  - Gate semantics become per tick.

**Keeping it as a documented deviation.**
- It costs one tick of free hero input after each lock or cutscene. `MovePlayer` early exits depend on flags set by map events (`0x34` mask, the damage entry). The first tick of a map entry stays unguarded (O-E19-80 family).
- Every D1 oracle comparison and arc test then models a ±1 tick shift, so proofs against the binary are approximate.
- The hit pass is unaffected within the entity block: events(N+1) still follow hits(N).
- Nothing existing moves.

Recommendation (I): fix it, and measure first with a spike swapping the order in the existing harnesses and listing the moved pins. The author decides with the contract.

## 6. Draft E20.a contract

**Outcome.** One logic tick has the binary's structure.
- `MovePlayer` runs at every tick on the `TickPad` words.
- The counters pass and the hit hook run once per tick over all entities, in the order of section 4.
- The per-frame workarounds are gone.
- The inversion is resolved (A) or documented (B).
- The first-frame cases are probed.

**Scope.** `AlundraEntityScriptProxy.cs` (tick loops, hero branch), `AlundraWorldProxy.cs` (tick loop, hooks), `AlundraPlayerManager.cs`, `AlundraTickPad.cs`, `AlundraPlayerController.cs` (`AlundraPadState.ButtonsReleased`), `AlundraEventProgramRunner.cs` (`0x2F` onto `TickPad`), `AlundraGameState.cs` (`TickPad` owner), the three harnesses (`IntroTraceHarnessTests`, `HeroTraceHarnessTests`, the rigs), plan and ADR.

**Non-goals.**
- `PlayerTryAttack`, the run and the item use: stubs `TryUseItem`=1, `PlayerTryAction`=0, `PlayerTryAttack`=0, called in binary order (§0.2.2). The prologue calls (`0x8002F49C`, `0x8002F768`, `0x8002F884`) are stubs.
- Hit logic (E20.c1) and the hero damage entry.
- Dropping the per-frame `SyncAnimation` (D-E19-13).
- Clearing `RidingEntity` in the counters pass.
- Engine changes, `CasaEngine.Launcher/Program.cs`, the converter and the export.

**Tasks.**
1. Spike: swap the map-event/entity order in the intro and arc harnesses, and list the moved pins (no commit). Author decides fix or keep.
2. Extend `AlundraPadState` with `Released`, add `FromTickPad`, and move `0x2F` modes 0/1 to `TickPad` (modes 2/default may follow).
3. Shared per-entity tick body, with the world-driven flag (option A) or a hero loop (option B).
4. Hero per tick: `MovePlayer`, `StepAnimationClock`, `Tick`, then `UpdateVramFlags`, `UpdateGroundSlope` and `UpdateFloorHeight` inside the tick. Delete `JumpStartTickStamp` and its guard.
5. Counters pass, lists per tick when gated, and an empty hit hook at the end of the entity block.
6. Probe the real game (E19.v2 method) on a New Game and on a `0x52`/`0x53` arrival; apply the floor rule only if the case shows.
7. Update the tests and the traces; ADR for the tick structure and the inversion decision.

**Acceptance.**
- Existing suites green, with each changed pin explained against the binary (SJ6b to 155136; SJ5b and SJ5c with Cross held until the tick; SJ4/SJ4b removed).
- New `AlundraTickStructureTests`, with 0, 1 and 2 ticks per frame:
  - `MovePlayer` calls equal the tick count.
  - Each common pass runs once per tick, in the binary's order.
  - A hit-hook value written at tick k is visible to the events of k+1 and cleared by the counters.
  - 0 ticks leaves the hero unchanged.
- Hero and intro traces re-measured, with each delta explained.
- The sonde du vrai jeu is unchanged on the New Game.

**Stops.**
- A moved pinned intro point or chain arc that the binary does not justify.
- A first-frame regression in the sonde.
- An existing test that must change without a binary reason.
- Option A causing more than a bounded number of rig rewrites. Fall back to B and document.
- The author declining to choose fix or keep.
