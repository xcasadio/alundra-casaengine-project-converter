# E19.h1b2 discovery - surface DLL / tests / corpus at HEAD `21db852` (read-only)

Nothing was built, run (outside scratchpad), edited, staged or committed in the repository. Scripts and outputs are under
`h1b2-disc/` (`corpus/`, `dll/`, `tests/`, `engine/`). Paths are repo-relative. `ESP` = `Alundra/Scripts/AlundraEntityScriptProxy.cs`,
`ESF` = `Alundra/Scripts/AlundraEntitySpawnFactory.cs`, `AWP` = `Alundra/Scripts/AlundraWorldProxy.cs`, `ASM` =
`Alundra/Scripts/AlundraScriptedMotion.cs`, `RUN` = `Alundra/Scripts/AlundraEventProgramRunner.cs`, `ITH` =
`Alundra.Tests/IntroTraceHarnessTests.cs`.

IMPORTANT staleness of the plan section 1.2n.1c: it was written before E19.h1b1 landed (ADR-0026, D-E19-59). Points (3) and (4) of
its "Conception proposee" are DONE at HEAD (spawn without the `+ 1`, relevee at `T`, production `immediateAtSpawn` calls removed). What
is left for h1b2 is narrower than the esquisse reads (see section 8).

---------------------------------------------------------------------------------------------------------------------------------

## 1. Facts: how 0x20, 0x22, 0x23 are handled at HEAD

- `RUN:1852-1853` `default: return UnknownOpcode(command, state);` ; `UnknownOpcode` `RUN:2717-2754` skips by the size table, kind
  `UnknownSkipped`, one warning per opcode. 0x20, 0x22, 0x23 have no `case` (the only Z-wait `case`s: 0x21 at `RUN:721-725`, 0x24 `:708`,
  0x25 `:715`, 0x26 `:727`, 0x47 `:731`, 0x48 `:736`, 0x6F `:741`). So they are SKIPPED, not partial: the program runs on through them
  as if they ended at once.
- Size table `Alundra/Scripts/EventOpcodeSizeTable.cs:69-72`: 0x20 size 3 "Wait Z distance", 0x21 size 3, 0x22 size 1 "Wait height target",
  0x23 size 1 "Wait height target or Z contact" (labels already renamed by H1-R2).
- `WaitZDistance` (`RUN:2476-2492`) is already the exact 0x20 core (shared with 0x21): first call at a pc (`Parameters[1] != CodeIndex`)
  memorises `CodeIndex` and `entity.PosZ` (`Parameters[2]`), returns 0; later `(|memo - PosZ| >> 16) >= (v1 | v2 << 8) ? 3 : 0`.
  A `case 0x20` is one line: `return WaitZDistance(entity, v, state);` (it already returns 3 or 0). The comment at `RUN:724`
  ("0x20 itself stays skipped until E19.h1b") becomes stale.
- Runner conventions: after a non-zero return `state.Parameters[1] = 0; state.CodeIndex += result` (`RUN:461-462`); `entity` of `Dispatch` is the
  LOGIC entity (`RUN:559`), the memo lives in the owner's state. A `case 0x22` needs the logic entity's record height.
- `IntroTraceHarnessTests.ImplementedOpcodes` (`ITH:319`) is a hand-kept mirror: 0x20, 0x22, 0x23 are absent ("0x21, 0x26, 0x47, 0x48, 0x6F
  // E19.h1 H1-1" is there). Only used for the `[implemented]` tag of `docs/intro-programs-389.txt` (`ITH:1777`); that file has 0 line
  naming 0x20/0x22/0x23 (grep), so the annex stays byte-identical.
- The audit oracle `AlundraStoryChainOpcodeAudit.ProbeOpcode` (`Alundra.Tests/AlundraStoryChainOpcodeAudit.cs:89-122`) runs `[op, 0.., 0xFF]` on a
  bare `new AlundraEntityScriptProxy()` and calls the op "ported" if the first trace record is not `UnknownSkipped`/`UnknownNoSizeTerminated`
  (an exception also counts as ported): a `case 0x22` that reads a missing record height must not make the probe fail in another way than
  by throwing or returning (both read "ported").

## 2. Facts: the proxy's record height and Z state

- There is NO record height on the proxy: `ESP:82` `//public SiEntityRecord? EntityRecord;` is commented out. The only place the record
  `Height` is read is `EntityRecordMapper.Map` (`Alundra/Scripts/EntityRecordMapper.cs:196-200`): `proxy.PosZ = height << 0x13; proxy.TileZ = PosZ >> 20`
  (raw pre-clamp elevation). After spawn (`ESF:656` `PosZ = PosZ - ModZ`, relevee `:661-668`) the raw value is gone. So 0x22 needs a new
  proxy member (e.g. `Height << 19`, with a "no record" state for the hero / bare test proxies: `default(int)` 0 would silently mean "target 0").
- Z fields on the proxy (`ESP`): `ForceZ` 285, `FinalForceZ` 292, `IsZForceApplied` 295, `PosZ` 300, `TileZ` 303, `FloorHeight` 312, `TerrainHeight` 313,
  `CollidedWithEntityZ` 363, `IsOnGround` 364, `MapGravityRaw` 488, `MapZViscosityRaw` 491, `ModZ` 389, `Depth` 390. `Clone` (`ESP` ~2352) copies
  `MapGravity`/`MapMaxFallSpeed` but not the two raw fields (they are set after the clone by the spawn, `ESF:431`).
- A controller-less proxy at HEAD, per logic tick (non-player loop `ESP:1209-1275`, order: `PickEventTrigger` + `RunPickedEvent` 1213, `StepAnimationClock`
  1220, `TickScriptedNpc` 1261-1263 (gated `Status.IsActive()`), `EvaluateEntitySupport(ScriptHost.Collidables)` 1274):
  - `ASM:440-537` (`RunOneKinematicTick`, called through `RunOneMotionTick` `ASM:178`): `FinalForceZ = ForceZ` (500); X/Y integrated directly `PosX += FinalForceX` (523, no wall, no field); `TileZ = PosZ >> 20` (537) BEFORE any Z step.
  - `EvaluateEntitySupport` (`ESP:641-937`): decay block gated `Controller != null && !immediateAtSpawn` (685-711) -> skipped; `TickForceZ = FinalForceZ` (715);
    `TerrainHeight = ComputeTerrainHeight()` (755, unconditional; 0 if `Owner.World` or its field is null); local `terrainHeight` kept 0 (758-761);
    seed `platformTopZSeed` unclamped `(PosZ+ModZ)+FinalForceZ` (770-775); support found -> `WasEntitySupportedLastTick`, `CollidedWithEntityZ = 1`,
    `PosZ = supportTopZ - ModZ`, `ForceZ = 0` with Gravity, `TileZ` (779-789), `PushLogicalPositionToRoot`/`IsOnGround = 1` only with a controller (791-795);
    not found -> everything (landing, `MoveVerticalAndPullPosition`, `IsOnGround`) behind `if (Controller != null)` (801-915); `SetExternalVerticalDisplacement` controller-only (934-937).
  - So a controller-less entity NEVER changes Z from `ForceZ`, never lands, never gets `IsOnGround` (stays 0), apart from the support snap above. `0x1B` on it writes `ForceZ` and nothing happens.
  - Root follows `PosZ`: `AlundraFrameSyncPasses.SyncTransform` (`AlundraFrameSyncPasses.cs:362-378`) writes `ResolveLogicalPosition(PosX,PosY,PosZ)` (pixel truncation) when `Controller == null`.
  - `PushLogicalPositionToRoot`, `MoveVerticalAndPullPosition` (`ESP:2172`) and the head pull (`ESP:1154-1170`) are no-ops without a controller.
- The proxy of a controller NPC (for comparison, the code a controller-less step would mirror): decay `ESP:685-711` (impulse `IZF << 8`, else Gravity decay
  `ForceZ -= MapGravityRaw << 8` bounded both sides by `MapZViscosityRaw << 8`); `terrainHeight = TerrainHeight` (758-761); seed clamped
  `Math.Max((PosZ+ModZ)+FinalForceZ, terrainHeight+1)` (770-775); landing `FinalForceZ < 1 && moddedPosZ + FinalForceZ <= landingTop - 1` with
  `landingTop = terrainHeight + 1` i.e. `moddedPosZ + F <= T` (839), landed `PosZ = T - ModZ`, `CollidedWithEntityZ` only on the strict `moddedPosZ + tickForceZ < T`
  (857-872), `ForceZ = 0` with Gravity, `IsOnGround = 1` (899); else `MoveVerticalAndPullPosition(F/65536f, ...)` + `IsOnGround = 0` (912-913).
  The DLL keeps the `<=` landing (D-E19-40); the hero R6 uses the binary strict test (`ASM` 195-232).

## 3. Facts: the spawn factory guard and the map gravity fields

- `ESF:603-606`: `if (proxy.Controller != null && tileMapData != null) { (proxy.MapGravity, proxy.MapMaxFallSpeed, proxy.MapGravityRaw, proxy.MapZViscosityRaw)
  = ResolveMapGravitySettings(tileMapData); proxy.ApplyGravitySettingsToController(); WalkabilityMask...; IsVerticalOwnedExternally = true; }`.
  `ResolveMapGravitySettings` `ESF:738`. The block is inside `ApplySpawnInitialization` AFTER the header check (`ESF:565-570`), so it only exists for a record whose header resolved
  (all 9741 records of the corpus have one: `corpus/` census, 0 without header).
- => every controller-less record has `MapGravityRaw = MapZViscosityRaw = 0` today (all spawn paths: map load `AWP:788`, `SpawnEntityByRecordId` `AWP:2593`, bare fallback `ESF:401`, harness `ITH:751`/`:1215`;
  every one passes `tileMapData`). Corpus: map `Gravity` 128 / `ZViscosity` 4096 on 481 maps, 3 / 256 on 159 and 160 (re-measured by `corpus/` run: unchanged); the 7 gravity controller-less records
  (11 rec33, 260 rec17, 398 rec9 = I33 broken armour; 41 rec1, 42 rec1, 47 rec0, 47 rec3 = Sara) are on maps 128/4096.
- Readers of the two raw fields at HEAD: `ESP:696-697` (controller-gated decay), `ASM:294-295` (hero state), `HeroTraceHarnessTests.cs:272-273` (sets them). Hero writes: `AWP:1800-1807`.
  No test asserts that a controller-less proxy has them at 0 (`grep MapGravityRaw|MapZViscosityRaw` in `Alundra.Tests`: only hand-set values, 0 reads of a spawned controller-less proxy).
- HYPOTHESIS (consistent with the design notes): split the gate so the four map fields are assigned whenever `tileMapData != null` and keep `ApplyGravitySettingsToController` /
  `WalkabilityMask` / `IsVerticalOwnedExternally` under `Controller != null`. KEY CONSEQUENCE (new, not in the 2026-10-03 notes): after the split the HARNESS entities (bare, `tileMapData` passed) hold 128/4096
  too, so nothing but the new "World has a field" criterion would stop a second decay in the harness: the criterion must gate EVERY new controller-less write, the decay included.

## 4. Facts: where a controller-less vertical step can run

- Not in `AlundraWorldProxy.Update` (`AWP:1974-...`): it holds no per-entity Z logic (pad ticks, directors, `RefreshUpdateProxiesAndCollidables` 2158, `RunPendingEventTriggers` 2173,
  wall interleave, camera) and runs AFTER every entity update (`World.cs` entity loop, then the world proxy; the comment at `AWP:1991`). The only per-entity Z site is the proxy's own tick loop:
  `ESP:1274` `EvaluateEntitySupport`. A new step is either a branch of `EvaluateEntitySupport` (the controller gates at 685, 758, 770-775, 791, 801, 934 become "controller or world-with-field") or a sibling method
  called at `ESP:1274` after it (but the support found/not-found split is inside `EvaluateEntitySupport`, so a sibling must re-derive it).
- Engine facts (read, `CasaEngineMonogame`): `World.AddEntity` only queues (`World.cs:168-173`); `Entity.World` is set at integration `World.cs:774-801` -> `Entity.InitializeWithWorld` (`Entity.cs:227-229`), before the entity loop
  `World.cs:520-548`; `Entity.Update` calls `GameplayProxy.Update` only when `World?.Game?.ExecutionPolicy.UpdateGameplayScripts` (`Entity.cs:505-508`). Hence `Owner?.World?.CollisionField == null` is true for: the intro harness
  (bare entities never added, `ESF:490-512`, `ITH:751`), bare-mode arcs (`ArcRun.OneFrame` updates entities one by one and never runs `world.Update`, `Alundra.Tests/AlundraArcSupport.cs:376-381`), direct `proxy.Update` tests with no world
  (`AlundraGlobalFreezeEntityUpdateTests.cs:124`, `:224`), and false for every entity a real `World.Update` ticks on a map with `AlundraCells` (all 483 export maps carry it; `TryCreate` success on all 483 NOT run).
- The exported controller-less prefabs (6 prefab ids, 67 records) are ticked every frame: prefab JSON `tick_policy: EveryFrame`, components `DepthSortable2DComponent` only, `script_class_name: AlundraEntityScriptProxy`
  (`alundra-project/Entities/<name>/<name>.entity`); engine default policy for such an entity is `DynamicDefault` (EveryFrame) too (`EntityPolicyResolver.cs:72-87`).

## 5. Facts: the intro harness at HEAD (re-checked line by line)

- Entities: every harness entity, load-time and dynamic, is `CreateBareEntityFromRecord(record, _catalog, tileMapData: ...)` (`ITH:751`, `:1215`) - bare, `Initialize()`d, never in a world: `Owner.World == null`,
  no controller, no root. `CreateBareEntityFromRecord` passes NO `collisionField` (`ESF:490-512`): no relevee at spawn in the harness (no floor, not even 0), PosZ = `z<<16 - ModZ` (since h1b1).
- Spawn support: `BuildCollidables` + `EvaluateEntitySupport(_collidables, immediateAtSpawn: true)` `ITH:764-765` and `:1233-1234`. Harness-only now (production calls gone).
- Frame `RunFrame` (`ITH:836-912`): `BuildCollidables`/`UpdateRidingEntities` 839-840 -> `entity.Update(FixedTickSeconds)` over the frame-start snapshot 874 -> map events -> `RunPendingEventTriggers` D3 catch-up -> `RunVerticalPhysicsPass(_spawnedEntities)` over the LIVE list 908.
- Pass `ITH:972-1062`: decay with `_mapGravityRaw`/`_mapZViscosityRaw` (the harness's own copies, `ITH:677`, `:679`; one-sided terminal) 992-1002; `FinalForceZ = ForceZ` 1004; own 4-corner probe 1006; seed `Math.Max(feet+F, T+1)` 1016; `landingTop = T + 1` 1017;
  landing `F < 1 && moddedPosZ + F <= landingTop - 1` -> `PosZ = landingTop - ModZ` 1029-1033; else `PosZ += F` 1040; `TileZ` 1054; `IsOnGround = landingTop >= PosZ` 1061. Binary convention (`T + 1`), no `IsZForceApplied`.
- WARNING (new): between `entity.Update` (874) and the harness pass (908) the D3 catch-up `RunPendingEventTriggers` runs, so any new write of `IsOnGround`/`TileZ`/`PosZ`/`CollidedWithEntityZ` that `EvaluateEntitySupport` makes for a bare harness proxy
  is visible to a `0x70`/`0x07` read in that pass before the harness overwrites it. Hence every new controller-less write must sit under the criterion (the existing found-branch writes of `PosZ`/`CollidedWithEntityZ`/`ForceZ`/`TileZ`
  already run for harness proxies today and the traces are green; they stay as they are).
- Criterion `Owner?.World?.CollisionField == null` re-checked: true for ALL harness entities; same shape as `ClampToGround` (`ESP:1506-1511`) and `ComputeTerrainHeight()` (`ESP:1579`); false in production for entities `World.Update` ticks.
  The 2026-10-03 design (option 1a: criterion, harness pass untouched) therefore still holds; the six traces and the pins `ITH:127-194` do not move by construction of the criterion, provided the criterion gates the decay too (section 3).

## 6. Hypotheses (rules a plan could write; each is a design choice, not a fact)

- R-20: `case 0x20: return WaitZDistance(entity, v, state);` (binary `0x8003D9BC`, emulated: `dll/handlers_emu.txt`).
- R-22: record height `Height << 19` memoised in `Parameters[2]` at the first call at the pc (returns 0); later calls: `PosZ == target` -> 1 (size 1); else `diff = target - PosZ`, `ForceZ` clamped to `diff` when
  (`diff > 0 && diff < ForceZ`) or (`diff <= 0 && ForceZ < diff`); returns 0. LITERAL target, no convention conversion (ADR-0026 names this exception). Binary-exact, see `dll/handlers_emu.txt`.
- R-23: `0x22` first (memo and clamp always happen), then `CollidedWithEntityZ != 0` -> 1.
- R-REC: new proxy member for the record height (set in `EntityRecordMapper.Map` only when `Height` parses), plus the no-record state (the binary prints "No InitData." and reads byte 9 of a null record: garbage, no halt; no site in the corpus; technical choice: log once and wait).
- R-GATE: split `ESF:603` as in section 3.
- R-STEP: under `Controller == null && Owner?.World?.CollisionField != null && !immediateAtSpawn` (and `Status.IsActive()` like `ASM` motion, D-E19-29), run the same decay / clamped seed / found-landing-else logic as the controller NPC with the displacement
  `PosZ += FinalForceZ` instead of `Controller.Move`, `IsOnGround` 1 on support/landing and 0 otherwise, `TileZ = PosZ >> 20` after the step. One shared rule (the `<=` landing of D-E19-40) costs nothing: no existing pin on a controller-less Z or `IsOnGround`.
  Strict test (binary-exact, as hero R6) is also free for the same reason but would make two landing rules coexist.
- R-TH: write `TerrainHeight` in `ApplySpawnInitialization` when a field is handed (the value `terrainHeight` is already computed at `ESF:662`; binary `0x80039EF8`); closes the h1b1 verifier's note (`0x8D`, `RUN:1828-1850`,
  reads 0 before the entity's first `EvaluateEntitySupport`, which runs AFTER the pick/run of the same tick at `ESP:1213` vs `1274`). No test pins `TerrainHeight` after a production spawn (only hand-set `AlundraEventProgramRunnerTests.cs:4521/4542/4564`).

## 7. Engine (question 4)

- No engine change is needed for the literal `0x22` target and none for controller-less entities in the update loop (details `engine/notes.md`).

## 8. What is left of the esquisse at HEAD

- (1) harness coexistence: criterion confirmed, plus the new "gate the decay and every new write" requirement. (2) gravity gate: still open (R-GATE). (3) and (4) DONE by h1b1. (5) `0x22` target: ADR-0026 already names the literal target as an exception; engine side confirmed clean.
- `0x20` is not "skipped on two controller-less entities only": porting it alone would stall 47 `C[4] @442` and 260 `C[5] @747` forever (their `0x1B` never moves Z today). The port of the waits and R-STEP are one inseparable change.
