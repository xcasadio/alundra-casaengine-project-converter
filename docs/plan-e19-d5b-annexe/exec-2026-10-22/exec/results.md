# E19.d2c2 D5b: measured results (task by task)

Plan: `docs/plan-e19-opcodes.md` section 1.2h.3.2b. Start HEAD `a24273b3`. T0 is `state-T0.md`. This file is written task by task; this first version is T1.

## T1 - the witnesses, red first (no production line)

### The test aids

| aid | where | what |
|---|---|---|
| `ContactHost.PhysicsPassId`, `ContactHost.BeginPhysicsPass()` | `Alundra.Tests/Support/ContactHost.cs` | the emulation of the pass number of D-D5B-1. A montage is self-driven (one tick per frame, hero first), so a pass is a frame: `JumpHeroRig.Update` and `ContactWorld.Integrate` call `BeginPhysicsPass()` once per frame before the update of the world. Nothing reads it yet: T4 adds the default member of `IScriptHost` that this property then implements. |
| `ContactHost.Rebuild` computes `RidingEntity` of the non-heroes, the hero excluded | same file | as `WorldProxy.RefreshUpdateProxiesAndCollidables` at the lists step: the passengers are cleared, `EntitySupport.UpdateRidingEntities(Collidables)` runs, the hero keeps the value of its own tick head (the first attempt of the discovery, hero included, turned UH-10, UH-15 and UH-17 red; none moved here). |
| `EndPhysicsTick` called at the end of the frame by the montage; `TickPhysicsOnce` called by the self-driven pilots | **not written in T1** | both methods are production code that does not exist before T3 and T4 (the plan forbids production lines in T1). The call from the montage (`JumpHeroRig.Update`, `ContactWorld.Integrate`, one line each) comes with the task that adds the method; the pilots (`EntityScriptProxy.RunSelfDrivenTicks`) are production code and are T4's. |

Existing tests that moved because of the aids: none (the three partitions below, 5 627 existing tests, 0 failure, Release and Debug).

### The new files

| file | tests |
|---|---|
| `Alundra.Tests/Hero/HeroPlatformRideTests.cs` | 9 (theory rows counted): `UH16_...Rising`, `UH16_...Descending`, `E22_...`, `DESC_...`, `UH19_...` (2 rows), `Dock_...Rising`, `Dock_...Descending`, `Freeze_...` |
| `Alundra.Tests/Entities/PassengerPlatformFollowTests.cs` | 13 (theory rows counted): `CrateA_...` (4 rows: before or after the platform, rising or descending), `NpcA_...`, `RestA_...`, `ImpulseA_...`, `PileZA_...`, `PileXYA_...`, `MarkedB_...` (2 rows), `PileZB_...`, `PrefabC_...` |
| `Alundra.Tests/Entities/RidingEntityCountersPassTests.cs` | 2: `RideM4_...` (the carrier read right after the counters pass; the carrier removed between two ticks) |
| `Alundra.Tests/Arcs/LiftMap22ArcTests.cs` | 3: `A_D5b_22_FromTheLiftItself_...`, `A_D5b_22_TheRecipeOfTheAuthor_...`, `A_D5b_22_ThePile...` |

27 tests (rows counted), written with the values of `vec_d5b_out.txt` (E22, DESC, CRATE, NPC, PILE_Z), of `check/t_land_on_rising.py` (UH-19), of `work/out/t5_dock_real.txt` (dock) and, for the pile in XY, the corrected
form of D-D5B-7 (no binary value: the binary has the double force). The relations are all relative (`rel`: the foot minus the top of the carrier), the same in the DLL's convention as in the binary's.

### The red list (26) and the task that makes each green

All 26 fail at the start HEAD, each on the assertion of its own mechanism (the message of the first failure is in the last column). "Assigned" is the plan's assignment; a witness can turn green earlier
(it then stays green as a guard).

| witness | assigned | first failure at the start HEAD |
|---|---|---|
| `RideM4_APassengerOnItsCarrier_HasNoCarrierRightAfterTheCountersPass_...` | T2 | tick 0: the barrel still has its carrier of the previous tick |
| `RideM4_TheCarrierRemovedBetweenTwoTicks_...` | T2 | the barrel's `RidingEntity` read at `"lists"` is not null |
| `UH16_APlatformRisingByOnePixelPerTick_...` | T3 | tick 1: foot minus top -65536 |
| `E22_TheLiftRisingByOnePixelPerTickFromTick3For64Ticks_...` | T3 | tick 3: foot minus top -65536 |
| `A_D5b_22_FromTheLiftItself_...` | T3 | f12 (rise 1): foot minus top -65536 |
| `A_D5b_22_TheRecipeOfTheAuthor_...` | T3 | wait 0 (rise 1): foot minus top -1835008 (the hero is at the bottom of the hollow) |
| `Dock_ABlockRisingByOnePixelPerTick_...` | T3 (the entry rule, D-D5B-3) | tick 1: foot minus top -65536 |
| `Freeze_APlatformWithANonZeroForceAndARider_...` | T4 (guard of the ceiling; it can be green from T3, before the ceiling exists) | the final foot minus top -2359296 (the platform never stood still, the hero is left behind) |
| `UH16_APlatformDescendingByOnePixelPerTick_...` | T4 | tick 1: foot minus top +65536 |
| `DESC_TheLiftDescendingByOnePixelPerTickFromTick3For24Ticks_...` | T4 | tick 3: foot minus top +65536 |
| `Dock_ABlockDescendingByOnePixelPerTickToTheDock_...` | T4 | tick 1: foot minus top +32768 |
| `UH19_...(gap: 65536, rise: 196608, ...)` | T4 | tick 0: the platform rose 196608 (it must stop under the foot: 32768) |
| `UH19_...(gap: 16384, rise: 65536, ...)` | T4 | tick 0: the platform rose 65536 (16384 expected, with the contact 1) |
| `CrateA_...(crateAfterPlatform: True, force: 65536)` | T6A | tick 3: foot minus top -65536 |
| `CrateA_...(crateAfterPlatform: False, force: 65536)` | T6A | tick 3: foot minus top -65536 |
| `CrateA_...(crateAfterPlatform: True, force: -65536)` | T6A | tick 3: foot minus top +65536 |
| `CrateA_...(crateAfterPlatform: False, force: -65536)` | T6A | tick 3: foot minus top +65536 |
| `NpcA_AnNpcOf20By14By32WithGravityOnARisingPlatform_...` | T6A | tick 3: foot minus top -65536 |
| `RestA_ACrateAtRestOnAPlatformWithNoGravity_...` | T6A | tick 0: collided 1 (the binary: 0, the crate copies the zero force of its carrier) |
| `PileZA_TheHeroOnTheCrateOnTheRisingPlatform_...` | T6A (the hero's copy is T3; the middle crate is T6A) | tick 3 crate: foot minus top -65536 |
| `PileXYA_ThePlatformMovingEastAndUp_...` | T6A (+ T5 for the hero's XY) | tick 3: crate dx to the platform -65536 |
| `A_D5b_22_ThePile...` (real barrel, map 22) | T6A (+ T3, T5) | f1: barrel dx 0 |
| `MarkedB_...(force: 65536)` | T6B | tick 3: foot minus top -98304 |
| `MarkedB_...(force: -65536)` | T6B | tick 3: foot minus top +32768 |
| `PileZB_AMarkedObjectOnACrateOnTheRisingPlatform_...` | T6B (needs T6A for the crate under it) | tick 3 crate: foot minus top -65536 |
| `PrefabC_ABrokenArmourI33WithNoController_...` | T6C | tick 3: foot minus top -98304 |

`UH-14` is not in the list: it is an existing test, green at the start HEAD, re-pinned by T5 (`-tick x 65536`).

### The planned negative witness (green at the start HEAD)

`PassengerPlatformFollowTests.ImpulseA_APassengerThatTakesAnImpulse_DoesNotCopyTheForceOfItsCarrier_ItFliesByItsOwnImpulse` is green before the code: the passenger ignores its carrier today, and the binary also
ignores it when the passenger has an impulse (`+0xF8 != 0`; `vec_d5b.py` with `impulse=True`: `fz = ffz = 327680`, never the carrier's 65536). It is the guard that T6A must keep green (a copy of the force that
overwrote the impulse would turn it red). It is the only test of the new files that is green. Only the force of the tick is pinned: the DLL's root pull of a supported entity that takes off comes one tick late
(`EvaluateEntitySupport`, `wasSupportedEnteringThisTick`), a quirk outside D5b.

### The guards (existing tests, green at the start HEAD and to stay so)

The plan's section 4 of the discovery report lists them; all ran green with the new aids, in Release and in Debug: `HeroObjectTopsTests` (UH-6, UH-10, UH-11, UH-14 until T5, UH-15, UH-17, UH-18 x2, SJ-12,
SJ-13), `AbsoluteZWritesTests`, `NpcCharacterControllerMoverTests`, `TickStructureTests` (A8), `LogicalZStepTests`, `LogicalZWorldTests`, `ControllerlessVerticalStepTests`, `FloorHeightTests`,
`TerrainHeightTests`, `NoGravityLandingTests`, `AnimationImpulseTests`, `HeroApexAndCeilingTests`, `HeroFallAndPadJumpTests`, `LadderClimbTests`, `EventProgramRunnerTests`, `ClassAndBranchOpcodesTests`,
`EntitySearchServiceTests`. No existing test is red: the "predicted list of moving tests" of T1 is empty, and the one test that the discovery's prototype moved (UH-14) moves with T5.

### The runs

Release with the neutralised copy, three partitions (`--blame-hang-timeout 60s`), then Debug (built last, without the copy neutralised), same partitions:

| partition | tests | failures (Release) | failures (Debug) |
|---|---|---|---|
| `Arcs`, `Ai.`, `Audio`, `Cameras`, `Cells`, `Combat`, `Data`, `Dialogues` | 1 213 (1 210 + 3 new) | 3 (the three new arcs) | 3 |
| `Entities`, `Events`, `ExportGuards`, `Hero`, `Items` | 2 652 (2 628 + 24 new) | 23 (all new) | 23 |
| everything else | 1 789 | 0 | 0 |
| total | 5 654 (5 627 + 27 new) | 26 | 26 |

The 26 are exactly the red list; the 5 627 existing tests are green. `python -B tools/dll-structure/check_names.py --rules`: 0 finding (1 warning, existing: `TextBoxGpu.cs`).
The four `docs/hero-trace-389-*.txt` files were rewritten by the suite with other line endings and restored by `git checkout` (never indexed).

## T2 - M4 (`RunCountersPass` clears `RidingEntity`, D-D5B-6)

One production line (`proxy.RidingEntity = null;` in `WorldProxy.RunCountersPass`, `0x800389DC`) and the stale doc of the method corrected. The carrier is recomputed right after at the lists step
(`RefreshUpdateProxiesAndCollidables`) and the hero's at the head of its own tick, so no reader sees the cleared value outside RIDE-M4.

Red before, green after: `RideM4_APassengerOnItsCarrier_HasNoCarrierRightAfterTheCountersPass_...` and `RideM4_TheCarrierRemovedBetweenTwoTicks_...` (2 tests). Red list after T2: 24 (26 - 2), the planned negative
witness (`ImpulseA_...`) stays green. Existing tests moved: none. Release with the neutralised copy: the three partitions give 24 failures, exactly the red list minus RIDE-M4 (the partitions by name overlap, as the counts
of T0 showed); `Hero`/`Entities` partition 2 886 tests (24 red), `Arcs` etc. 1 317 (3 red, the new arcs), the rest 1 564 (0 red). The traces of the hero were restored by `git checkout`.

## T3 - M1, the recursion, the entry rule and M3 for the hero (D-D5B-2, -3, -5)

Production (Release and Debug, no engine file touched):
- `EntityScriptProxy.PeekTickForceZ` is the ONE function of the vertical law (`ApplyVerticalForceLaw` now writes its result); `CarrierTickForceZ(depth)` gives the force a carrier hands its passengers at the current tick: its
  `TickForceZ` when it already moved in this pass, else, for a carrier that is itself carried and has no impulse, the force of ITS carrier (recursion, bounded to 8), else its own peeked law.
- `ScriptedMotion.RunHeroVerticalTick`: the carried hero takes `CarrierTickForceZ()` for both `ForceZ` and `FinalForceZ` (M1); the entry rule (D-D5B-3): a hero carried by an entity whose force of the tick is not 0 enters the
  state held by the tick (`restOnEntity`), whatever the engine says of the ground.
- M3: `EntityScriptProxy.EndPhysicsTick` re-reads the hero's `IsOnGround` after the physics loop (`WorldProxy.RunEntityBlock`, after every move, same `BlockedByEntity` filter as the loop); the value computed by the tick stays
  (it is what the later ticks of a self-driven frame and the rest of the tick read) and the end pass overwrites it on a host that tracks the passes (`IScriptHost.PhysicsPassId != 0`).

Deviations from the plan's split (all in the same task, none changes an acceptance):
1. D-D5B-1 (`IScriptHost.PhysicsPassId`, `WorldProxy.PhysicsPassId`, `EntityScriptProxy.TickPhysicsOnce` at the world loop and the two pilots) lands in T3, not T4: `CarrierTickForceZ` needs "the carrier already moved in this
   pass" to give the same value before and after the carrier moved. T4 adds `EnsureMovedThisPass`, the ceiling of the carriers and the `out` entity of `TryFindCeiling`.
2. The pilots (`RunSelfDrivenTicks`) guard only the FIRST tick of a self-driven frame with `TickPhysicsOnce` (`TickSelfDrivenPhysics`): the pass identifier of a montage names a frame, and the later ticks of a frame of several
   ticks (each entity plays its whole frame alone) are outside any pass. A first attempt guarded every tick and turned four existing tests red (`HeroWaterIceTests.UW6`, `HeroJumpTests.UJSND`,
   `HeroFallAndPadJumpTests.SJ6`, `SJ6b`: the second tick of a two-tick frame was skipped); SJ6b also showed the in-tick `IsOnGround` must stay (the second tick reads the first's), hence the "read again" form of M3.
3. Test aids: `ContactHost.EndPhysicsPass()` (calls `EndPhysicsTick` on every proxy), called by `JumpHeroRig.Update` and `ContactWorld.Integrate` after the update of the world (the T1 plan of the aids).
4. `DockMontage` (T1 witness `Dock_*`): the block is now added AFTER the hero, like the platform of `Montage()`. With the block added first (the `configure` callback) it ticks before the hero, which then finds its carrier already moved
   at the head of its tick (the binary's prepass `0x800364C8` runs before every move; the world also ticks the hero first, `WorldProxy._tickEntities`); the probe of the discovery (P15) shows the same 1 px artefact. The
   assertions are unchanged. Class doc of `HeroPlatformRideTests` corrected (the dock rising is made green by T3).

Red before, green after (Release, neutralised copy): `UH16_APlatformRisingByOnePixelPerTick_...`, `E22_TheLiftRisingByOnePixelPerTickFromTick3For64Ticks_...`, `Dock_ABlockRisingByOnePixelPerTick_...`,
`Freeze_APlatformWithANonZeroForceAndARider_...` (green from T3, as planned, since there is no ceiling yet), `A_D5b_22_FromTheLiftItself_...` (the arc A-D5b-22: zero gap from image 12 to the warning),
`A_D5b_22_TheRecipeOfTheAuthor_...` (6 tests). Red list after T3: 18. Existing tests moved: none (UH-14 still green until T5; UH-18 unchanged).
Release, the three partitions: 2 886 tests / 18 red (the 18 witnesses), 1 317 / 1 red (the pile arc), 1 564 / 0. The four traces of the hero and the intro trace (their guard tests are in the partitions) are identical; the files restored by `git checkout`.

## T4 - M2 and the ceiling of the carriers, one commit, the four vertical sites (D-D5B-1, -4)

Production:
- `EntityScriptProxy.EnsureMovedThisPass(target)` (the binary's "move the target first", `0x800376B8-DC` and `0x80037630-50`): runs `target.TickPhysicsOnce()` when the target has not moved in the pass, belongs to it (a host that
  drives the ticks: `TookPartInFrame` or `JoinsBirthTick`; a self-driven montage: every entity), is not blocked by another entity (`BlockedByEntity`, E20.e0c) and is not the caller. `TryFindSupportMovingFirst` and
  `TryFindCeilingMovingFirst` repeat the search while the entity found is moved first. `EntitySupport.TryFindCeiling` gives the collidable that lowered the target (new overload; the old signature is kept).
- The four sites: the hero (`RunHeroVerticalTick`, support and ceiling), the marked entities (`RunLogicalZPreXyStep`, support and ceiling; the ceiling used to see an empty list, O-E0K-6, and now sees the collidables),
  the entities with a controller (`EvaluateEntitySupport`, support and the rise of the "not found" tail), the entities without a controller (`StepVerticalWithoutController`, support and rise; the absolute ceiling is the same
  search, `TryFindCeiling` gives both).

Red before, green after (Release, neutralised copy): `UH16_APlatformDescendingByOnePixelPerTick_...`, `DESC_TheLiftDescendingByOnePixelPerTickFromTick3For24Ticks_...`, both rows of `UH19_...`,
`Dock_ABlockDescendingByOnePixelPerTickToTheDock_...` (the 9 hero witnesses are green: with `Freeze_...`, `UH16` rising, `E22`, `Dock` rising, T3). Existing tests moved: none (partitions: 14 / 1 / 0 red, all witnesses of T1).

Deviations and findings:
1. `Dock_ABlockDescending...` could not turn green in the T1 montage: the block's physical box is over the 16 px floor, so `PushLogicalPositionToRoot -> ClampToGround` lifts the block back to 16 px at its landing at the bottom of the hollow
   (measured: the block went 1 -> 16 inside the nested move), and the engine snaps a block within `GroundSnapDistance` (4 px) of the ground under its root. `DockMontage(hollowUnderRoot: true)` (descending only) puts the cells under the root
   box (cells 5 and 6, rows 5 and 6; the hero at x 193 never meets them) in the hollow too and sets the block's `GroundSnapDistance` to 0. The assertions are unchanged; the rising dock keeps the original field (at rest in the air over a
   hollow ground under its root the engine would snap the block down).
2. STOP CONDITION OF THE PLAN, met by design until T6A: with the ceiling of the carriers, a platform whose `ForceZ` is not 0 and which carries a NON-HERO passenger stands still, because that passenger does not follow yet (the copy of
   the force of the path A is T6A, of the path B T6B, of the path C T6C; the prototype had the path A copy in the same patch). Witnesses concerned, all red since T1 and still red: `CrateA_*` (the 2 rising rows: `platform 0`),
   `PileZA`, `PileXYA`, `NpcA`, `MarkedB(+65536)`, `PileZB`, `PrefabC`; and the NEGATIVE guard `ImpulseA_*`, green until T3, red now with `tick 3: platform 0`: the crate takes its impulse at the tick 3 but the DLL's pull of a supported
   entity that takes off keeps `PosZ` pinned to the support (`EvaluateEntitySupport`, found branch, one tick of delay), so the crate is still a ceiling for the platform. T6A must therefore also stop the found branch from pinning an entity whose
   force of the tick is positive (the binary searches a support only for `F <= 0`), or the guard stays red. The hero case (the plan's "gel") is green: `Freeze_*` passes, no hero stands still more than 2 ticks.
   Judgement: the plan lists the crate witnesses as red after T4 and T6 as the task that follows, so I did not treat this transient as the plan's stop (which names the freeze witness); it is flagged for the reader, T4 and T6A are to be merged together.
3. `EnsureMovedThisPass` in a self-driven montage runs the target's physics before its own programs and clock of the tick (the target's own loop then skips the physics of the first tick of the frame). In a world the programs and clocks
   of every entity precede the physics, as in the binary.

Release, the three partitions: 2 886 tests / 14 red, 1 317 / 1 red, 1 564 / 0. The traces of the hero and of the intro are identical; the four files restored by `git checkout`.

## T5 - M5, the planned force of the carrier for the hero (D-D5B-2, -7, -11)

Production (`Alundra/Physics/ScriptedMotion.cs`, no engine file touched):
- `PlanForce` is the ONE function of the horizontal force of a tick (target and step of the animation, then the increment); `RunOneKinematicTick` calls it with `commit: true` and writes the force, `PeekAdjustedForce` calls it
  with `commit: false` (writes nothing). `KeyAnimationOf` is the animation key of the tick of an entity that is not the hero, shared by `TickScriptedNpc` and the peek.
- `PeekAdjustedForce(carrier)`: the force the carrier used when it already moved in this pass, else its planned force (0 for a carrier that is not active or blocked by another entity).
- The hero carried by an entity adds `PeekAdjustedForce` of its carrier instead of `LastTickDeltaX/Y` (the binary, 0x80037364-0x800373B0): phase 0.

Tests: `UH14_...` re-pinned to the binary (`hero.PosX - heroStart == -tick * 65536`, was `-(tick - 1) * 65536`, D-E20-40), renamed `..._CarriesTheHeroInPhase0_...`; new `UH14b_...` (4 directions: the peeked force equals the force the carrier then uses,
over 12 ticks through a recompute, the acceleration and the arrival). `PillarAiPitWorldTests.W6_PushedEast_...`: `over.Count` 16 -> 15 (D-D5B-11), comment citing the decision and the cause, nothing else in the test.

Red before, green after (Release, neutralised copy): `UH14_...` (re-pinned to phase 0; red at the T4 HEAD with the new pin, green with the planned force), `UH14b_...` x4 (new). Existing tests moved: exactly two, `UH14_...` and
`W6_PushedEast_...`, both predicted (plan T5 and D-D5B-11); none else.
Real mutation (restore `LastTickDelta` in the passenger line, script, then undone, `git diff` of the file unchanged): UH14 and W6_PushedEast red; restored, green.

Release, the three partitions: 1 216 tests / 1 red (`A_D5b_22_ThePile...`, T6), 2 665 / 13 red (the `PassengerPlatformFollowTests` witnesses: CrateA x4, PileZA, RestA, MarkedB x2, ImpulseA, PileZB, PileXYA, PrefabC, NpcA), 1 785 / 0.
Total 5 666 tests, 14 red, all T6 witnesses (the same red list as after T4). `check_names.py --rules`: 0 finding. The four traces of the hero were restored by `git checkout`. Debug build (last): 0 errors.

## T6A - the passengers with a controller (path A, D-D5B-7)

Production (no engine file touched):
- `EntityScriptProxy.CopyCarrierForceZ` (the copy `0x80037364`, `0x800373BC-C8`): a passenger other than the hero without an impulse takes `CarrierTickForceZ()` of its carrier for `ForceZ` and `FinalForceZ`; with an impulse it keeps
  its own. Called by `EvaluateEntitySupport` right after `ApplyVerticalForceLaw`, for an entity with a controller (the marked entities have their own site, T6B; the entities without a controller, T6C). `TickForceZ` is taken after it.
- The rest of path A needed no new line: the "move the target first" and the ceiling of the carriers (T4) already sit on the support and the rise of `EvaluateEntitySupport`. The T4 finding 2 (the guard `ImpulseA_*` red because the
  crate with an impulse stays a ceiling for the platform) is closed by the copy alone: the impulse crate is moved first by the ceiling search of the platform and the guard is green again.
- `EntityScriptProxy.EndPhysicsTick` re-reads `IsOnGround` of a passenger with a controller that moved in the pass (not marked, `RidingEntity` set), on a host that tracks the passes.
- `ScriptedMotion.RunOneKinematicTick`: the XY step of EVERY passenger (not only the hero) adds the planned adjusted force of each carrier of its chain, once each (`PeekAdjustedForce`, depth bounded to 8), D-D5B-7. This line is shared by
  the three paths (the XY step is one function for all the entities that are not the hero's own tick); it lands here with the path whose witnesses pin it (`PileXYA_*`, the barrel of the map 22 pile arc).

Red before, green after (Release, neutralised copy), 10 witnesses: `CrateA_*` x4, `NpcA_*`, `RestA_*`, `PileZA_*`, `PileXYA_*`, `A_D5b_22_ThePile...`, and `ImpulseA_*` (the planned negative guard, red since T4, green again). Still red (T6B, T6C): `MarkedB_*` x2, `PileZB_*`, `PrefabC_*` = 4.
Existing tests moved: none. Release, the three partitions: 1 216 tests / 0 red; 2 665 / 4 red (the T6B and T6C witnesses); 1 785 / 0 red. The traces of the hero and the intro trace are identical (their guards are in the partitions);
the four `docs/hero-trace-389-*.txt` restored by `git checkout`.

## T6B - the marked entities (`HasLogicalZ`, path B)

Production (no engine file touched):
- `ScriptedMotion.RunLogicalZPreXyStep`: `CopyCarrierForceZ()` right after `ApplyVerticalForceLaw()` and before `TickForceZ` is taken (the same copy as path A, no impulse; with an impulse the entity keeps its own).
  The "move the target first" and the ceiling of the carriers of this site were done by T4 (`TryFindSupportMovingFirst`, `TryFindCeilingMovingFirst`).
- `EntityScriptProxy.EndPhysicsTick` no longer skips the marked entities: the end pass re-reads `IsOnGround` of every passenger with a controller that moved in the pass (the post step of the marked entity read it before its
  carrier had moved when the carrier comes later in the order of the cases). The call `UpdateGroundSlope()` of E20.f6a did not move.

Red before, green after (Release, neutralised copy): `MarkedB_*` x2 (rising and descending), `PileZB_*` (a marked object on a crate on the platform; the crate is T6A's). Existing tests moved: none.
Release, the three partitions: 1 216 tests / 0 red; 2 665 / 1 red (`PrefabC_*`, T6C); 1 785 / 0 red. The four traces of the hero restored by `git checkout`.

## T6C - the entities without a controller (path C, PREFAB-C)

PREFAB-C exists (T0 section 5, T1): I33 broken armour on the montage of `ControllerlessVerticalStepTests`, so T6C does not stop and D5b affirms the form with C (O-E19-81 is not needed for the vertical).

Production (no engine file touched):
- `EntityScriptProxy.EvaluateEntitySupport`: `CopyCarrierForceZ()` is called for the entities that step their own Z without a controller too (the condition of the call already covered `stepsWithoutController`); `StepVerticalWithoutController`
  reads `ForceZ` for its force of the tick, so the copy is the force the carrier hands over. The "move the target first" and the ceiling of this site were done by T4.
- `ReadGroundWithoutController`: the end-of-step ground rule of `StepVerticalWithoutController` extracted unchanged (the terrain raised to the top of the entity below, `PosZ <=` it), shared with `EndPhysicsTick`, which reads it again for
  a passenger without a controller that stepped in the pass (`Status.IsActive()`, a collision field), once its carrier has moved. Without it the passenger that comes before its carrier in the order of the cases kept `IsOnGround` 0
  (measured: `PrefabC_*` failed `tick 3: onGround 0` with the copy alone).

Red before, green after (Release, neutralised copy): `PrefabC_ABrokenArmourI33WithNoController_OnARisingPlatform_FollowsItInTheSameTick`. Existing tests moved: none (`ControllerlessVerticalStepTests` green).
Release, the three partitions: 1 216 / 0 red, 2 665 / 0 red, 1 785 / 0 red: 5 666 tests, 0 failure; the red list of T1 is empty (26 red at the start, 27 witnesses, the planned negative guard green). The four traces of the hero restored by `git checkout`.

## T7 - preset, documentation, stale comments, ADR

- `Alundra/SaveGames/TestSaves.cs`: `new Preset("elevator-m22", "test-elevator-m22", 22, 46, 8, 2, [], [], [], [])`, the 13th, after `herbe-m14`. `TestSavesTests`: `ElevatorPresetName` in `AllPresetNames`, the order test renamed
  `TheThirteenPresets_AreListed_InTheOrderOfTheTable`, and `TheElevatorPreset_IsTheNewGameAtTheRightHandLeverOfMap22` (map, tile, no flag, the new game's counters, the rules accept it); the theories over every preset gain a row each.
  `docs/test-saves.md`: the "Lift preset" section with the recipe of D-D5B-10 (a start already on the platform is impossible: the lever state is temporary).
- The arcs `A_D5b_22_FromTheLiftItself_...`, `A_D5b_22_TheRecipeOfTheAuthor_...` and `A_D5b_22_ThePile...` are green since T3 and T6A; the pile arc is the one of the barrel of the map 22.
- Stale comments corrected: the doc of `EntitySupport.UpdateRidingEntities` (`RidingEntity` no longer feeds only the search types 5/6), the head-of-tick comment of `ScriptedMotion.RunOneMotionTick` (the other passengers' carrier is cleared by the counters
  pass and recomputed at the lists step), the doc of `EntityScriptProxy.LastTickDeltaX/Y` (a rider no longer reads it), the doc of `RunLogicalZPreXyStep` and of `StepVerticalWithoutController` (the ceilings of the other entities are ported). The doc of
  `WorldProxy.RunCountersPass` was done in T2.
- ADR-0088 (the highest number on any branch was 0087; `main` and `origin/main` stop at 0081) and its index row. `python -B tools/dll-structure/check_names.py --rules`: 0 finding (1 warning, existing: `TextBoxGpu.cs`).
- Counts, Release with the neutralised copy, the three partitions: 1 216 / 0 failure, 2 665 / 0, 1 789 / 0: 5 670 tests (the 5 666 counted at the end of T6C plus 4 of the preset: its test and the three rows it adds to the theories over every preset), 0 failure. The four traces of the hero and the intro trace identical
  (`git diff --ignore-cr-at-eol` of `docs/hero-trace-389-*` is empty; the files restored by `git checkout`). Debug (built last, no neutralised copy): the SaveGames, passenger, lift, hero-ride and pillar tests, 377 / 0 failure.
