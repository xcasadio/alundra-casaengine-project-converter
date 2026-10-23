# E19.d2c2 D5b, T0: the sites re-read at the start HEAD

Plan: `docs/plan-e19-opcodes.md` section 1.2h.3.2b (revision 1, READY). Start HEAD: `a24273b3` (branch `chantier/theme1-porter-plateforme`, after E20.e0c, E20.e, E20.f4, E20.f5,
E20.f6a and E20.g5a to g5d). The discovery (`../report.md`) was made at `1d0b6953`. Everything below is read by symbol at the start HEAD; no production line was changed.

## 1. The four gaps and the ceiling, by symbol

Files under `Alundra/`. SM = `Physics/ScriptedMotion.cs`, ES = `Physics/EntitySupport.cs`, EP = `Entities/EntityScriptProxy.cs`, WP = `Maps/WorldProxy.cs`.
"Plan" is the line the plan (and the report, section 1) gave at `1d0b6953`.

| gap | symbol | start HEAD line | plan line | state |
|---|---|---|---|---|
| M1 (hero) | `ScriptedMotion.RunHeroVerticalTick`: `hero.ForceZ = carrier.ForceZ; hero.FinalForceZ = carrier.TickForceZ;` | SM:321-322 | SM:313-325 | present, unchanged |
| M1 (XY of the hero) | `ScriptedMotion.RunOneKinematicTick`: `entity.FinalForceX += carrier.LastTickDeltaX` (and Y) | SM:681-682 | SM:676-681 | present |
| M1 (`LastTickDelta` written, read by the magnet gates) | `RunOneKinematicTick` writes `LastTickDeltaX/Y`; `SnapHeroAtApex` and `SnapAtApex` read it | write SM:719-720; reads SM:429, SM:571 | SM:717-718; SM:427, :569 | present |
| M1 (`TickForceZ` written) | marked entities `RunLogicalZPreXyStep`: `entity.TickForceZ = entity.FinalForceZ`; the others `EvaluateEntitySupport`: `TickForceZ = FinalForceZ` | SM:478; EP:802 | SM:476; EP:799-803 | present |
| M2 (hero, support) | `RunHeroVerticalTick`: `TryFindSupport(hero, ..., out _, ...)` | SM:339 | SM:330-342 | present (`out _`: the carrier is already found, not used) |
| M2 (hero, ceiling) | `RunHeroVerticalTick`: `TryFindCeiling(hero, hero.ScriptHost?.Collidables ...)` | SM:362 | SM:360 | present |
| M2 (marked, support) | `RunLogicalZPreXyStep`: `TryFindSupport(entity, ..., out _, ...)` | SM:489 | SM:484-491 | present |
| M2 (marked, ceiling, empty list) | `RunLogicalZPreXyStep`: `TryFindCeiling(entity, Array.Empty<EntityScriptProxy>(), ...)` | SM:506 | SM:504 | present (O-E0K-6) |
| M2 (controller, support) | `EntityScriptProxy.EvaluateEntitySupport`: `TryFindSupport(this, collidables, platformTopZSeed, out _, ...)` | EP:884 | EP:883-884 | present |
| M2 (controller, rise, no ceiling of entities) | the "not found" tail of `EvaluateEntitySupport`: `MoveVerticalAndPullPosition(FinalForceZ / 65536f, ...)`, `IsOnGround = 0` | EP:1018-1019 | EP:1018-1019 | present |
| M2 (no controller) | `EntityScriptProxy.StepVerticalWithoutController` (starts at EP:1092): rise `PosZ += force` (no ceiling of entities), support `TryFindSupport(...)` | EP:1100-1115 (support EP:1121), floor EP:1143 | EP:1100-1115, :1121, :1143 | present |
| M3 (hero) | `RunOneMotionTick`: `if (entity.ZHeldByTick) entity.IsOnGround = ...` | SM:229-232 | SM:225-230 | present |
| M3 (marked) | `RunLogicalZPostXyStep`: `entity.IsOnGround = ...` | SM:550 | SM:548 | present |
| M3 (controller) | `EvaluateEntitySupport`: `IsOnGround = 1` (found), `1` (terrain landing), `0` (still moving) | EP:900, 1005, 1019 | EP:900, 1005, 1019 | present |
| M3 (no controller) | `StepVerticalWithoutController`: `IsOnGround = PosZ <= floor ? 1 : 0` | EP:1148 | EP:1148 | present |
| M3 (head-of-frame pull of the hero) | `Update`: `IsOnGround = Controller.IsGrounded ? 1 : 0` and the flush-top rule | EP:1487, 1497-1501 | EP:1487, 1497-1501 | present |
| M3 (no pass after the physics loop) | `WorldProxy.RunEntityBlock`: the loop `_tickEntities[i].TickPhysics()` is followed by `RunActiveEffectsPass` and the hit pass only | WP:3001-3007 (trace `"physics"` WP:2999) | WP:2977-2980 | present; the loop now skips an entity with `BlockedByEntity != null` (E20.e0c) |
| M4 | `WorldProxy.RunCountersPass` does not clear `RidingEntity` | WP:3027-3050 (doc just above it, WP:3017-3026, stale: "`+0x12C` is NOT cleared ... until the riding is recomputed in the tick") | WP:3000-3023 | present |
| M4 (the recomputation) | `RefreshUpdateProxiesAndCollidables` -> `EntitySupport.UpdateRidingEntities(_collidables)` (every collidable with `Gravity` and not `NoRiders`, the hero included); the hero again at the head of its tick, `RunOneMotionTick` -> `EntitySupport.UpdateRidingEntityOfHero` | WP:3791; SM:197-200 | WP:3687-3700 | present |
| ceiling of the carriers | `EntitySupport.TryFindCeiling` (does not give the entity that lowered the target) | ES:200 | ES:200-263 | present |
| no pass identifier | `IScriptHost` has only `DrivesEntityTicks` (WP:826: `_world?.Game != null`) | IScriptHost.cs:96 | IScriptHost.cs:96 | present |
| the self-driven pilots | `EntityScriptProxy.RunSelfDrivenTicks`: `TickPhysics()` for the others and for the hero | EP:1527, 1546 | EP:1527, 1546 | present |

`rg "PhysicsPassId|EndPhysicsTick|PeekTickForceZ|PeekAdjustedForce|CarrierTickForceZ|EnsureMovedThisPass|TickPhysicsOnce" Alundra` finds nothing: no gap is already closed by a former slice.
No site is missing: the stop condition of T0 does not hold.

Stale comments to correct in T7 (lines at the start HEAD): ES:288-296 (doc of `UpdateRidingEntityOfHero`), WP:3017-3026 (doc of `RunCountersPass`), SM:193-194 (the hero's carrier recomputed at the head of every tick),
EP:279-281 (doc of `LastTickDeltaX/Y`: "one tick of phase").

Things that moved since the discovery and that the later tasks must keep in mind:
- `WorldProxy.RunEntityBlock` (E20.e0c): between the counters pass and the physics loop there is the clock pass and the rewrite of `ModdedPosX/Y/Z` for the active, unblocked entities (WP:2976-2997, traces `"clock"` and `"physics"`). Both
  loops skip an entity blocked by another (`BlockedByEntity`). The hero's `RidingEntity` is also recomputed at the head of its own tick (SM:197-200), after that rewrite.
- `EntityScriptProxy.TickMotion` (E20.f6a, ADR-0087): after `ScriptedMotion.TickScriptedNpc`, an entity marked `HasLogicalZ` calls `UpdateGroundSlope()` (EP:1630-1637); `EvaluateEntitySupport` follows.
  D5b's end-of-pass `IsOnGround` of the marked entities must not move that call.
- The montages `JumpHeroRig` and `ContactHost` are self-driven: `ContactHost` does not override `DrivesEntityTicks` (false), so each entity runs all its ticks of the frame in its own `Update`
  (`RunSelfDrivenTicks`, hero first, then the entities in the order they joined the world) and a frame of the montage is one pass. `ArcRun` with `RealController` has a `Game` and follows the production order.

## 2. The vectors replayed

`docs/plan-e19-d5b-annexe/work/` and `check/` were copied to a scratch folder with `exec-2026-10-22/annex-delta/work/` laid over `work/` (the scripts write next to themselves and read the binary
`ALUN_CD.EXE` at the absolute path they name). `python -B -X utf8 vec_d5b.py > out` ran in 1 s and the output is equal to `../vec_d5b_out.txt` (145 lines; compared with the line endings normalized).
`check/t_land_on_rising.py` replayed too: the two cases of UH-19 are the ones the plan quotes (1 px above, +3 px per tick: the platform rises 32 768 at the tick 0, then 196 608, rel 0, carried from the tick 1;
0.25 px above, +1 px per tick: 16 384 with the contact 1 at the tick 0, then 65 536). The values of the dock are in `work/out/t5_dock_real.txt` (rel 0 at every tick, rising and descending; the contact 1 from the tick 10).

## 3. The test counts at the start HEAD

Release with the neutralised copy (`-p:CustomAfterMicrosoftCommonTargets=.../skipcopy.targets` on build and test), whole `Alundra.Tests` in the three partitions of g5a/g5b (a full run is over 10 minutes),
`--blame-hang-timeout 60s`:

| partition (filter on `FullyQualifiedName`) | tests | failures |
|---|---|---|
| `Arcs`, `Ai.`, `Audio`, `Cameras`, `Cells`, `Combat`, `Data`, `Dialogues` | 1 210 | 0 |
| `Entities`, `Events`, `ExportGuards`, `Hero`, `Items` | 2 628 | 0 |
| everything else | 1 789 | 0 |
| total | 5 627 | 0 |

The Debug run (built last, without the copy neutralised) gives the same counts for the existing tests: it was made with T1's files present (see `results.md`), 5 627 existing tests green.
The four `docs/hero-trace-389-*.txt` files are rewritten with other line endings by the suite and were restored with `git checkout` (`git diff --ignore-cr-at-eol --quiet -- docs/` is clean).

## 4. The presets of the start HEAD

`Alundra/SaveGames/TestSaves.cs`, `TestSaves.Presets` (line 106): 12 presets, in this order: `day3-after-dream`, `day4-meeting`, `arsenal-a-m14`, `arsenal-b-m14`, `steel-m14`, `demon-m14`, `fire-m303`, `arrow-m16`, `swim-m14`,
`pickup-m11`, `loot-m14`, `herbe-m14` (the last, `WeaponPreset("herbe-m14", ...)` at line 145). `elevator-m22` will be the 13th, after `herbe-m14`.
`Alundra.Tests/SaveGames/TestSavesTests.cs`: `AllPresetNames` at line 80 (`[Day3.Name, Day4.Name, .. WeaponPresetNames, SwimPresetName, .. PickupPresetNames, GrassPresetName]`), the order test is
`TheTwelvePresets_AreListed_InTheOrderOfTheTable` at line 96 (the plan's `:74` and `:90` have moved; the test name carries the count and T7 renames it).

## 5. PREFAB-C: the prefab and the montage

The plan speaks of "12 prefabs sans contrôleur"; the census of the E19.h1b2 discovery (`docs/plan-e19-h1b2-annexe/corpus-notes.md`, section 2) gives **6 prefab ids, 67 records**, all with a resolved header
(the plan's "12" comes from the h1b2 line quoted at section 1.2n of the plan; the corpus notes are the measure, and both agree on 67 records). Only two of the six carry `Gravity` and `Collidable`, so are
passengers in the sense of the binary (`(flags & 0x4100) == 0x100`): **I33 broken armour** (maps 11 rec33, 260 rec17, 398 rec9) and **Sara (dream)** (maps 41 rec1, 42 rec1, 47 rec0 and rec3), both a point box
(size 0, 0, 0). The others (Laser, Rancune de Melzas, invisible key trigger, Nirude) have no gravity or no collision and never ride.
The montage that plays the vertical step of a controller-less entity (`EntityScriptProxy.StepVerticalWithoutController`, `EntityScriptProxy.cs:1092-1149` at the start HEAD) is the one of
`ControllerlessVerticalStepTests`: `ContactWorld.BuildWorld(new FlatGroundField { GroundZ = 0 }, null)`, `ContactWorld.AddEntity(..., withController: false)`, `ReadCollisionField() != null` from the world's field.
T1 writes PREFAB-C on it with I33 broken armour: `PassengerPlatformFollowTests.PrefabC_ABrokenArmourI33WithNoController_OnARisingPlatform_FollowsItInTheSameTick`.

## 6. Other facts found

- `TickTraceForTests` (`WorldProxy`) calls `"lists"` right after `RunCountersPass` and before `RefreshUpdateProxiesAndCollidables` (`RunEntityBlock`, WP:2965-2968): RIDE-M4 reads `RidingEntity` there, on the real
  world of an arc, so no test hook has to be added to the production code.
- The DLL's root pull of an entity that takes off from a support is one tick late (`EvaluateEntitySupport`, `wasSupportedEnteringThisTick`): the impulse of a passenger changes `TickForceZ` at the tick of the switch but its
  `PosZ` only at the next tick. Pre-existing, outside D5b; the impulse witness pins the force of the tick only.
- A passenger rooted at the same point as the platform is fine, but the HERO'S root must stay away from the roots of the objects (the hero's capsule blocks the move of a kinematic body that rises into it): the pile
  montage offsets the logical boxes, as UH-14 does.
- ADR numbers: the highest on any branch is `0087` (this branch; `main` and `origin/main` stop at `0081`). T7 recounts before writing the D5b ADR.
