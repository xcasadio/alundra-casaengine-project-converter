# E19.h1b2 discovery - tests at HEAD `21db852` (read-only)

Paths relative to `Alundra.Tests/`. Line numbers re-measured at HEAD (the plan's `~` anchors are stale: h1b1 and later slices moved them).
Nothing was built or run. "Moves" below is reasoned from the code read (inputs, gates, assertions), not measured.

## 1. The `immediateAtSpawn` callers and the files the plan names, re-checked

Callers of `EvaluateEntitySupport(..., immediateAtSpawn: true)` at HEAD (grep over `Alundra/` and `Alundra.Tests/`): production none (the doc comment `AlundraWorldProxy.cs:1780` only
mentions it); tests/harness:

| File:line | What follows / the assertion | Moves under h1b2? |
|---|---|---|
| `IntroTraceHarnessTests.cs:765` and `:1234` | harness spawn support, bare proxy, `Owner.World == null` | no (criterion false) |
| `AlundraNpcCharacterControllerMoverTests.cs:1842` | `Assert.Equal(expectedPosZ, proxy.PosZ)` (1846, `expectedPosZ = platform.PosZ + ModZ + Depth + 1` = 26214400 on the real 389 export), root Z 400 (1847), `WasEntitySupportedLastTick` (1848), then 60 `world.Update` keep PosZ/root/Gravity/supported (1865-1868); sailor 11 HAS a controller; the platform is a controller-less `BuildRealRecordProxy` (1735) that is only a `Collidables` candidate, never ticked | no |
| `:1949` | `Assert.Equal(expectedPosZ, proxy.PosZ)` (1950), then 60 direct per-tick `EvaluateEntitySupport(host.Collidables)` keep it (1955) | no |
| `:2019` | `pinnedPosZ == platform.PosZ + ModZ + Depth + 1` (2024), then a west walk and the support loss (2061 `settledPosZ`) | no |
| `AlundraMovementObstacleProbeTests.cs:384` | `Assert.Equal(26214401, sailor.PosZ)` (385) and `WasEntitySupportedLastTick` (386), 20 `Integrate` keep both (390-396); the platform (`rig.Add(..., controller: false)`, 371) is hand-set at PosZ 24117249, no gravity, field 48 px | see 3 |
| `AlundraTerrainHeightTests.cs:102` and `:117` | only `TerrainHeight` = the field height (`Round(10*65536)`, `Round(15*65536)`) | no if the step keeps `!immediateAtSpawn` |

`AlundraJumpTestSupport.cs`: `JumpNpcRig` takes `World`, `Host`, `Npc` as `required init` members (lines 27-35), so a controller-less montage needs no change in the file: `new JumpNpcRig { World = world, Host = host, Npc = npc }` over
`ContactWorld.AddEntity(..., withController: false)` (`AlundraContactTestSupport.cs:129-177`, 3 callers only) is how `AlundraCollidedWithEntityZTests.cs:64-75` already does it; gravity is set by hand
(`Flags |= Gravity; MapGravityRaw = 128; MapZViscosityRaw = 4096`, as `AlundraJumpTestSupport.cs:138-139, 174-175`) because `AddEntity` clears `Gravity` (`:140-146`).
Real-prefab montage for a controller-less spawn: `AlundraNpcCharacterControllerMoverTests.BuildRealRecordProxy` (1735-1751: `ApplyRecord` + `ApplySpawnInitialization(record, backing, proxy, catalog, tileMapData: ...)`) - the exact seam where the gate lift (R-GATE) becomes visible; its callers (1818, 1939, 2011, 2188, 2189) never tick the proxy.

## 2. Must change (certain), closed list

1. `IntroTraceHarnessTests.cs:319` `ImplementedOpcodes`: add `0x20, 0x22, 0x23` (label only; `docs/intro-programs-389.txt` has no line naming them, `intro-trace-389.txt` unchanged: 0x20/0x22/0x23 do not occur on map 389).
2. Comments only: `Alundra/Scripts/AlundraEventProgramRunner.cs:724` ("0x20 itself stays skipped until E19.h1b").
3. Nothing in `AlundraStoryChainSkippedOpcodesTests`/`AlundraStoryChainOpcodeAudit`/`Data/story-chain-skipped-opcodes.tsv` (see 4).

New tests a plan would add (values writable BEFORE the code, model or emulation):

- 0x20, shape of `AlundraZWaitOpcodesTests.cs:11-110` (opcode at pc 1 behind a `0x01`, `NewRunner`, `StateFor`, logic entity != owner case): the binary rows are in `dll/handlers_emu.txt` (real code of `0x8003D9BC` run):
  `[16,0]` from PosZ 3145728: call 1 -> 0 with memo (CodeIndex 1, Parameters[2] 3145728); 4194303 -> 0; 4194304 -> 3; 2097152 -> 3 (downward); 2097153 -> 0; `CollidedWithEntityZ` 1 alone -> 0 (unlike 0x21);
  `[0,1]` (256 px): 19922943 -> 0, 19922944 -> 3. Rouge today: skipped -> `(1, 0x20, 3)` at the first call, `Parameters[2]` stays 0.
- 0x22 (binary rows, record Height 20 -> 10485760): first call memo `Parameters[2] = 10485760`, result 0, `ForceZ` untouched; (PosZ 10485000, ForceZ 32768) -> ForceZ 760; (10485000, 700) -> 700; (10485760, 0) -> 1; (10485761, -5) -> ForceZ -1; (10485761, 0) -> 0, 0 (no push: a stall in the binary);
  (10000000, -32768) -> unchanged; (10000000, 600000) -> 485760; PosZ == target with contact flag -> 1. 0x23 (Height 30 -> 15728640): contact ends it even far from the target (result 1) but the clamp still ran; first call result 0.
  Rouge today: skipped -> size 1 advance, memo never written.
- Real-data arc, model of `AlundraAbsoluteZWritesTests.cs:26-68` (`ArcRun` with `RealController: true, Prefabs: true`, `OnInstruction`, `RunUntil`): a lift that runs `0x1B` then `0x20`, e.g. map 22 rec 2 ("Plateforme flottante (grande)", C program 131, `0x1B [0,1] @744`, `0x20 [64,0] @747`, then `0x53` change map at 756; `+1 px/tick` so the wait passes at the 65th pass counting
  the `0x1B` pass as the 1st and PosZ = start + 64 px); needs the hero riding it (`0x3E @731`). Any other of the 233 controller sites works (list: `corpus/sites-0x20-0x22-0x23.tsv`). Value = start PosZ + 4194304 (model: `n = 64*65536/65536 = 64` steps).
- Controller-less step (new), montage `ContactWorld.BuildWorld(FlatGroundField{GroundZ=0}, null)` + `AddEntity(withController: false)`: no-gravity entity with `ForceZ = -32768` (the Sara pattern `0x1B [128,255]`): PosZ falls 32768 per tick (192 ticks = 96 px; `0x20 [96,0]` ends at pass 192 after the `0x1B` pass); with gravity (`MapGravityRaw 128`, `ZViscosity 4096`): `ForceZ` after tick n = -32768 n bounded by -1048576, landing at `T`; rest on terrain: `PosZ == T`, `IsOnGround 1`; entity authored above `T` without gravity: `IsOnGround 0`, PosZ unchanged. All by arithmetic (no simulation needed); cross-check optional by the binary emulation of `ComputeZPosition` as in the h3 slice.
- Harness/criterion guards: a bare proxy with `Flags |= Gravity`, `MapGravityRaw 128`, no world -> `EvaluateEntitySupport` leaves `ForceZ`/`PosZ`/`IsOnGround` untouched (rouge if the criterion is dropped from the decay); the six traces and `ITH:127-194` pins byte-identical.
- Real-prefab gate test (R-GATE): `BuildRealRecordProxy`-style spawn of 260 rec 17 (I33, Gravity, no controller) or 47 rec 3 (Sara, Gravity, no controller, map 47) with the map's `TileMapData` (not the `AlundraAnimationImpulseSpawnTests.Spawn` seam, which passes no `tileMapData`, `AlundraAnimationImpulseSpawnTests.cs:65-66`): `MapGravityRaw 128`, `MapZViscosityRaw 4096` (both maps 128/4096, re-measured); rouge today 0/0.
- Record height (R-REC): `EntityRecordMapperTests.cs` (existing `Height` tests ~62/113/174/189 assert `PosZ`/`TileZ` only): new assertion `RecordHeight == Height << 19` independent of `ModZ` and of the terrain relevee.
- Spawn `TerrainHeight` (R-TH): montage of `AlundraAbsoluteZWritesTests` "relevee au terrain" (synthetic flat 16 px field through `SpawnEntityByRecordId`): `TerrainHeight` 1048576 right after the spawn (today 0). Value written in advance: the same `T` the test already pins for `PosZ`.

## 3. Existing tests that exercise a controller-less proxy in a world with a field (could move; reasoned: expected unchanged)

The test code that matters is "a non-player proxy, `Controller == null`, `Status` active, `ScriptHost` set, `Owner.World.CollisionField != null`, ticked by `Update` or `EvaluateEntitySupport(..., false)`":

- `AlundraTerrainHeightTests.cs:129-146` `PerTick_ControllerLessEntity_TracksFinalPositionEachTick` (`BuildEntity(world, false)` + `InitializeWithWorld`, `PositionalCollisionField` height = X, PosZ 0): the step would LAND the entity at T (3276800, then 5242880) - the test reads `TerrainHeight` only (3276800, 5242880: unchanged, `TerrainHeight` is computed from XY before the Z step). Not moved unless the plan puts the step in a sibling method (then not even exercised).
- `AlundraTerrainHeightTests.cs:96-106` `AtSpawn_ControllerLessEntity_SetsTerrainHeight`: `immediateAtSpawn: true` -> step skipped; unchanged.
- `AlundraCollidedWithEntityZTests.cs:64-90` `UJ4_ANpcSupportedByAnEntityAtN...`: the platform (`withController: false`, z 0, flat ground 0, no gravity, 32 px tall) would get `IsOnGround 1`; NPC assertions (`WasEntitySupportedLastTick`, `CollidedWithEntityZ 1` x3, then 0/0/false after `platform.Status = FlagToDestroy`) do not read the platform; the step must skip `FlagToDestroy` (`Status.IsActive()`), as the motion tick does.
- `AlundraMovementObstacleProbeTests.cs:367-393` `TRegZ_...`: the platform (controller-less, PosZ 24117249 = 368.0000153 px, field 48 px, no gravity, F 0) is far above `T`: no landing, `IsOnGround 0`; sailor assertions `PosZ == 26214401`, `Null(ObstacleOf)`, supported x20 unchanged (the platform's PosZ is what `TryFindSupport` reads: unchanged).
- Arcs (all in `RealController: true` mode integrate their entities; bare mode never does): see `corpus/notes.md` section 4.

Not exercised (no world / no field): `AlundraGlobalFreezeEntityUpdateTests.cs:124`, `:224` (direct `Update`, `Owner.World == null`), `HeadlessIntroSimulation` users (`AlundraCellStoreProductionTests`, `AlundraDialogueOpcodesProductionTests`, `AlundraGlobalFreezeEntityUpdateTests`, `AlundraSoundOpcodesProductionTests`, `Map389LoadProgramsTests`), `HeroTraceHarnessTests` (hero only).

## 4. The skipped-opcodes TSV and its rules

- `Data/story-chain-skipped-opcodes.tsv`: 102 data lines, maps 10/135/162/169/174/176/181/183/391/476, opcodes 0x87, 0x90-0x95, 0xA0, 0xA2, 0xA3 only. NO line for 0x20/0x22/0x23 (no site on the 30 chain maps).
- Rule 2 (`AlundraStoryChainSkippedOpcodesTests.cs:50`, stale lines) cannot fire; Rule 1 (`:42`) and Rule 3 (`:58`, `WaitOpcodes` `AlundraStoryChainOpcodeAudit.cs:52` includes 0x20-0x23) stay green: they only look at sites REACHED on the chain, and there is none.
  The oracle test (`:103`) asserts 0x05/0x00/0xFF ported and one absent-size opcode not ported: independent of 0x20/0x22/0x23. `Rule3_*Fabricated*` (`:233-241`) passes `_ => false`/`op == 0x20` as its own oracle: independent of the runner.
  Rule 5 (`AlundraDay3SceneArcTests.cs:34-43`, arcs that wire it) flags any `UnknownSkipped` of an arc trace: none of the arcs crosses these opcodes.
- So the TSV and its tests need NO change; porting the three opcodes only makes the oracle say "ported" for them.

## 5. Tests that name the three opcodes

`grep` for `0x20|0x22|0x23` in `Alundra.Tests`: only `AlundraStoryChainOpcodeAudit.cs:52`, `AlundraStoryChainSkippedOpcodesTests.cs:235-241` (fabricated), unrelated hex constants (cell bits, glyph codes, Yarn). No test asserts that one of the three opcodes is skipped; no test runs a real program containing one (maps loaded by tests: 389, 390, 391, 392, 476, 478, 416, 163-165, 172, 176-179, 185, 135, 10, 15, 346, 61, 26, 27, 28, 34, 137, 140, 321, 323, 337, 411, 445, 83, ... ; of the 81 maps with a site only 27, 28 (single real instruction bytes `0x35`/`0x6E` isolated by `AlundraEventProgramRunnerTests.cs:3915`, `:4128`, never executing 0x20) and 44 (backdrop only, `BackdropStagePsxSemiTransparencyTests.cs:37`) are loaded).
