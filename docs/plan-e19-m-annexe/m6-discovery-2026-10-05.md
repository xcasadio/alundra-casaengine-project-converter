# E19.m6 discovery: remaining test hygiene (M-19 x3, M-31, M-32 overlay half, m1 advisory)

Read-only discovery, 2026-10-05. Repository `D:\development\repo\alundra-casaengine-project-converter`, branch `chantier/e19-opcodes`,
HEAD `d353136`. Nothing was built, run, exported or edited in the repository; no git state changed (the engine submodule's uncommitted
`CasaEngine.Launcher/Program.cs` was not touched). Plan = `docs/plan-e19-opcodes.md` at HEAD (section "#### 1.2s.6 E19.m5", "Reportés",
lines 7612-7615). Backlog = `docs/plan-e19-m-annexe/backlog-2026-10-03.md` (rows M-19 :44, M-31 :56, M-32 :57). Scripts and outputs of this
folder: `slide_model.py` / `slide_model.out.txt` (16.16 model of the DLL slide, validated on T-SL1 and T-SL3), `binary_check.py` /
`binary_check.out.txt` (the binary's `ComputeXYPosition` port `e19h-disc/B/cxy.py` on the new montages).

**No scope file changed since the census.** `git log bafbd5a..HEAD` is empty for `AlundraPlayerManagerTests.cs`,
`AlundraHeroJumpStatesTests.cs`, `AlundraLadderClimbTests.cs`, `AlundraHeroSlideTests.cs`, `AlundraCellVisualSyncTests.cs`,
`AlundraEventProgramRunnerLogicEntityTests.cs`, `AlundraJumpTestSupport.cs`, `AlundraContactTestSupport.cs`, `AlundraCellVisualSync.cs`,
`WallPlacementOverlay.cs`; only `AlundraTurnOrderTests.cs` is newer (created by E19.m1, `d3e206f`). E19.f2a touched none of them. The slide
code (`AlundraScriptedMotion.cs:557-668`) is unchanged since h4 (`a09cd66`): the diff since then has no hunk in `SlideAlongWall`/`BlockedCorners`.
Every line below is re-located at HEAD.

## Summary table

| Item | Open? | Where (HEAD) | Exact test change | Values in advance | Scripted mutation that must turn red (survives today) | Montage / helpers | Ownership | Recommendation |
|---|---|---|---|---|---|---|---|---|
| M-19a name | Open | `AlundraPlayerManagerTests.cs:95-110` | Rename `MovePlayer_OtherAnimationId_LeftUnchanged_NotPortedCase` (e.g. `MovePlayer_AirStillOnTheGround_GoesBackToMoving_AnUnportedAnimationIsLeftUnchanged`); body and comment unchanged | n/a | none (name only; no unique kill: SJ3 `AlundraHeroJumpStatesTests.cs:194-220` and the LoadingMap tests `:164-198` kill the same mutations) | none | free | Rename (P4) |
| M-19b SJ4b | Open | `AlundraHeroJumpStatesTests.cs:263-271` | Rename (e.g. `SJ4b_ATakeOffWithoutAStampOfThisTick_IsRewrittenByTheTail_EvenOnAFrameWithoutTick`) and fix the comment (`:266` says "walking", the hero is `JumpStanding`; `MotionTickCount - 1` is -1, the field's "no stamp" default, `AlundraEntityScriptProxy.cs:268`) | n/a | none (no unique kill: SJ3b `:222-233` covers 2/0x2B with stamp -1, SJ4 `:239-261` the rewrite after a tick) | none | free | Rename + comment (P4); deleting it as redundant = author's call |
| M-19c `% 32768` | Open (weak) | `AlundraLadderClimbTests.cs:423` | `Assert.Equal(-65536, previousForce)` in place of `Assert.True(previousForce < 0 && previousForce % 32768 == 0, …)` | **-65536 [derived, hypothesis]**: fall entered at the 1st update, decay -32768 per tick from 0 | `AlundraScriptedMotion.cs:292` `else if (gravity)` -> `else if (gravity && !fall)` (no decay on the entry tick) -> -32768: passes `:423` and the loop `:424-431` today; **but UH-7 (`AlundraHeroFallAndPadJumpTests.cs:355`) already kills it suite-wide** | existing | D5a closed this test; D5b (paused, F2 `IsOnGround` rule) could move the entry tick | **Close without change** (M-23 precedent: kills nothing new); keep only for self-sufficiency, measure first |
| M-31 sign S | Open | prod `AlundraScriptedMotion.cs:641` | New `TSL7_SouthAgainstTheCornerOfOneCell_…` (mirror of T-SL1) | yes (table below; model) | `:641` signs swapped -> tick 9 X 17186816 (262.25) instead of 17285120 | file's `Rig(OneCell, 263f, 135f)` + speed-312 anim set, Down held | free | Add |
| M-31 sign E | Open | prod `:653` | New `TSL8_EastAgainstTheCornerOfOneCell_…` (mirror of T-SL3) | yes (= binary) | `:653` signs swapped -> tick 12 Y 11567104 (176.5) instead of 11632640 | `Rig(OneCell, 203f, 177f)`, `SteadyWalk(24, 159744, 0)`, Right held | free | Add |
| M-31 entity gate | Open | prod `:595` | New `TSL9_ACornerBehindAnEntity_NeverSlides_ForceAdjustedFromTheNextTick` | yes (model) | `:595` without `entity.XCollisionEntity != null ||` -> tick 9 X 17285120, SlideCount 1, FA 0 | T-SL1 + `probeFactory: AlundraMovementObstacleProbe`, `configure: ContactWorld.AddEntity(…, "Wall", 250, 167, 0, -10, -7, 0, 24, 16, 32)` (UJ12 pattern `AlundraHeroJumpTests.cs:185-191`); the blocking cell is required | free (support files used read-only) | Add |
| M-31 agreement | Open | prod `:586` | New `TSL10_ADirectionThatDisagreesWithTheForce_KeepsTheForceAdjustedOfTheStep` | yes (FA = binary) | `:586` `if (!agrees)` -> `if (false)` -> tick 1 FA 0 instead of 1 | `Rig((cx, _) => cx <= 9, 250f, 200f)`, `SteadyWalk(16, -159744, 0)`, Up held | free | Add |
| M-31 slide in `MoveControllerAndPullPosition` | Open, **equivalent mutant in production** | only production callers: `AlundraScriptedMotion.cs:518`, `:663` | none required; optional contract test TSL11 (direct call after T-SL1's contact) | optional: X 17235968, SlideCount 0, FA 1 | `h4v_mut.py` `direct` -> SlideCount 1, X 17285120 (only a direct call sees it) | T-SL1 rig | free | Close as production-equivalent (or optional TSL11, P4) |
| M-31 oblique table term | **Unreachable in stage 1** | `:605-613` | none | n/a | `obl_table` is equivalent: corners are sampled at the position the engine accepted (`:609`, advance 0,0), always free | n/a | stage 2 (D-E19-58: not ported) | Close (record the reason) |
| M-31 UO-1 via `0x42` | Open | prod `AlundraEventProgramRunner.cs:666-673`; test UO-1 `AlundraHeroSlideTests.cs:208-222` (owner = logic) | New `NoObstacleSlide_0x46_0x45_ActOnTheHero_After0x42_NotTheOwner` in `AlundraEventProgramRunnerLogicEntityTests.cs` (its `FakeWorld` has `PlayerEntity`, `:28-47`) | hero 0x100 -> 0x2100 -> 0x100; owner unchanged; CodeIndex 2 then 1; Result kept | `:668` and `:672` `entity.Flags` -> `owner.Flags` -> hero 256 instead of 8448 | `FakeWorld`, pattern of `:260-277` | free (`dedd1f8` last) | Add |
| M-32 overlay half | Open | prod `AlundraCellVisualSync.cs:153-161` (`:157`); tests `AlundraCellVisualSyncTests.cs:736-880` | New fixture (2x8 + one load floor placement) and new test: overlay-at-load cell emptied, then refilled -> adopted | yes (exact entries) | `:157` drop `&& !sync._floorModel.ContainsKey((x, y))` -> refill degraded (0 entries, 1 "degraded" warning) | `CreateSyntheticComponent`, `WallPlacementOverlay.ApplyFloor`, `FloorPlacementRecords`, `CapturingWarningLogger` (all in the file or production) | free | Add (P3) |
| m1 advisory | Open (P4) | `AlundraTurnOrderTests.cs:24-36` vs `AlundraEventProgramRunnerTests.cs:38` (**private** nested) | none | n/a | none possible (montage only) | — | reuse = moving a private fake used 85 times + 3 private copies in dialogue files | **Close without change**: 28 private `IEntityWorldContext` classes in 27 test files is the repo's convention |

Closed list if the recommendations are taken: new tests TSL7, TSL8, TSL9, TSL10 (`AlundraHeroSlideTests.cs`), the 0x45/0x46 logic-entity test
(`AlundraEventProgramRunnerLogicEntityTests.cs`), the M-32 test and its fixture (`AlundraCellVisualSyncTests.cs`); existing code touched:
the two renames (M-19a, M-19b with its comment) and, if TSL9 goes through it, the file-private `Rig` helper of `AlundraHeroSlideTests.cs:46-52`
(two optional parameters). No production file: no export, no `cmp` change, six traces unaffected.

---

## M-19 (three sub-items outside D5b)

The fourth sub-item, the SJ-12 ternary `AlundraHeroObjectTopsTests.cs:373` (`tick == 18 ? hero.ForceZ == 0 : hero.ForceZ == 0`), stays with
D5b (plan :7615). "Branches without test" were never itemized (backlog :44): out.

### M-19a `AlundraPlayerManagerTests.cs:95-110`
- **Fact.** The name says `LeftUnchanged_NotPortedCase`; the comment (`:98-99`) says 0x2D is ported since E19.d2c2 S1 and the first assertion
  (`:105`) expects 0x2D -> 1 (Moving). Only the second half (`:107-109`, 0x20 unchanged) matches the name. Production: the jump-state branch
  `AlundraPlayerManager.cs:300-308` takes 0x2D; 0x20 falls through every case (`:417-418` comment).
- **Change.** Rename only (proposal in the table). The plan's history cites the old name (`:3692`, `:3942`): history is not rewritten.
- **Mutation.** None (a name). Both halves are already killed elsewhere: 0x2D + Right on the ground -> Moving is SJ3 (`AlundraHeroJumpStatesTests.cs:194-200`,
  `AirStill`); an "every other animation goes to the tail" mutation is killed by the LoadingMap tests (`AlundraPlayerManagerTests.cs:164-175`:
  0x36 with Up would become Moving). Value: readability (P4).

### M-19b `AlundraHeroJumpStatesTests.cs:263-271` (SJ4b)
- **Fact.** `hero = Hero(JumpStanding)`, `JumpStartTickStamp = MotionTickCount - 1` = -1 (the default "no stamp", `AlundraEntityScriptProxy.cs:268`,
  `MotionTickCount` starts at 0, `:357`); `Move` with no button -> Idle. The comment (`:266`) speaks of "the walking take-off"; the name describes
  the guard's motivation (a counterfactual) rather than what is asserted (a take-off not stamped at this tick IS rewritten).
- **Redundancy (fact by reading).** SJ3b (`:222-233`) asserts 2 and 0x2B with stamp -1 on the ground are rewritten (Right -> Moving); SJ4
  (`:239-261`) asserts the Idle rewrite after a tick, for both take-offs. Guard `AlundraPlayerManager.cs:303`
  (`!takeOff || player.JumpStartTickStamp != player.MotionTickCount`): `!=` -> `>` is killed by SJ4 and SJ4b; deleting `MotionTickCount++`
  (`AlundraScriptedMotion.cs:218`) is killed by SJ4, not SJ4b. No mutation found that only SJ4b kills.
- **Change.** Rename + comment ("standing", "a stamp of an earlier tick, or none (-1)"). No assertion change, no mutation. Deleting the test as
  redundant would be the author's call (not recommended in a hygiene slice).

### M-19c `AlundraLadderClimbTests.cs:423`
- **Fact.** `GameplayFreeze_MidFall_HeroHoldsItsHeight_ThenFallsOnWithTheSameVelocity` (`:403-457`): hero Idle 40 px above the ladder cell,
  two `world.Update`, then `Assert.True(previousForce < 0 && previousForce % 32768 == 0)`; the loop `:424-431` then pins the decay (-32768 per
  frame) and `PosZ += ForceZ`. The comment (`:420`) claims "from 0".
- **Derived value [hypothesis, measure first; a contradiction is a stop].** Update 1: the engine (gravity 1250, `GroundSnapDistance` 4) leaves
  the hero not grounded 40 px up; head pull `AlundraEntityScriptProxy.cs:1161-1172`: `IsOnGround` 0, `HeadPullGroundTrusted` true (vertical
  not external: no climb, pad neutral); `MovePlayer` posts Gravity; `RunHeroVerticalTick` `AlundraScriptedMotion.cs:251-279` enters the fall
  with `ForceZ = 0`, then `:292-297` decays to -32768. Update 2: -65536. So `:423` reads **-65536**.
- **Mutation.** `AlundraScriptedMotion.cs:292` `else if (gravity)` -> `else if (gravity && !fall)` (no decay on the entry tick): `:423` sees
  -32768, which passes today's assertion and the loop (decay still 32768 per frame) -> survives the class. **UH-7**
  (`AlundraHeroFallAndPadJumpTests.cs:326-375`, `:355` `ForceZ == -32768 * (tick - 14)` from the first fall tick, and `:353` PosZ 1015808)
  kills the same mutation suite-wide. A decay-rate mutation (`<< 8` -> `<< 7`/`<< 9`) is already killed by the loop.
- **Value.** Self-sufficiency of the test's own "from 0" claim; nothing new killed at suite scope. Same shape as M-23, closed without change
  by E19.m5 (plan :7610-7611). Risk: D5b's F2 `IsOnGround` rule (plan :3877 (7)) could move the entry tick and force a re-pin.
  **Recommendation: close without change**; if the main session keeps it, the value -65536 must be measured first.

## M-31 (h4 verifier's survivors, plan :5837-5840)

Verifier mutation script `scratchpad/h4v_mut.py` (and `h4v_mut4.py`): names `sign_s`, `sign_e`, `gateent`, `accord`, `direct`, `obl_table`.
Every pattern is still unique at HEAD (checked with `grep -F -c`: one hit each in `AlundraScriptedMotion.cs`; `return actual;` once in
`AlundraEntityScriptProxy.cs:2105`). The scripts run `dotnet test` on the whole project with a filter argument: reuse them, never commit.

**Model.** `slide_model.py` ports `RunOneKinematicTick` (force ramp, `AlundraScriptedMotion.cs:440-498`), the engine's first Move per axis
(X then Y, exact contact at the last free 16.16 position: `CharacterControllerComponent.cs:1230-1290` pre-probe + bisection, far edge
`BitDecrement` `:970-994` (doc `:952-969`); the hero fixture is `[root-10, root+11) x [root-7, root+8)`, `HeroWorldFixture.cs:85-89`, so between 128 and
256 px, where the float ULP is one 16.16 unit, the engine contact is exact on both signs), the Move's `ForceAdjusted`
(`AlundraEntityScriptProxy.cs:2080-2083`, eps 0.01 px), the probe (semi-open 16.16 overlap, `AlundraMovementObstacleProbe.cs:38-58`)
and `SlideAlongWall` (`:557-668`). **Validation: it reproduces every pinned value of T-SL1 (`:89-116`) and T-SL3 (`:137-155`)** (both
"True" in `slide_model.out.txt`). New values below are the model's: facts for the code paths, hypothesis for the engine's exact contact on the
positive directions (well founded, not run) -> measure first, a contradiction is a stop.

### TSL7 — sign of the south slide (`AlundraScriptedMotion.cs:641`)
- Montage: `Rig(OneCell, 263f, 135f)` (cell (10,10) blocks, x 240..263, y 160..175), anim 1 speed 312 accel 1 (the helper `ImposeNorthForceProfile`
  `:60` sets exactly that; its name says north: call `rig.SetAnimSet(new AnimSetEntry { Anim = 1, Speed = 312, Acceleration = 1 })` directly
  or rename the helper's doc), Down held, 25 ticks.
- DLL values (16.16): ticks 1-8 `PosY` 8927232, 9086976, 9246720, 9406464, 9566208, 9725952, 9885696, **9961472** (152.0, contact), `PosX`
  17235968; ticks 9-23 `PosX` = 17235968 + 49152 x (t - 8) (17285120 ... 17973248), `PosY` 9961472; tick 24 `PosY` 10121216, tick 25
  10280960; `ForceAdjusted` 0 every tick; `SlideCount` 15.
- Mutation `sign_s`: tick 9 `PosX` **17186816** (262.25), then -49152 per tick. Survives today: no test walks south into a single corner
  (T-SL1/T-SL2/UO-2 north, T-SL3 west, T-SL4/T-SL6 oblique).
- Binary (`binary_check.out.txt`): same rows except the known residue of its halving on positive directions (O-E19-28 b): contact 3 units
  short at tick 8 (9961469), 152.0 at tick 9, slides ticks 10-24. The test pins the DLL (stage 1, D-E19-58), its doc should say so, as A10J's.

### TSL8 — sign of the east slide (`:653`)
- Montage: `Rig(OneCell, 203f, 177f)`, `SteadyWalk(rig, 24, 159744, 0)`, Right held, 24 ticks (exact mirror of T-SL3).
- DLL values: ticks 1-10 `PosX` = 13303808 + 159744 x t (13463552 ... 14901248), `PosY` 11599872; tick 11 `PosX` **15007744** (229.0,
  contact); ticks 12-23 `PosY` = 11599872 + 32768 x (t - 11) (11632640 ... 11993088), `PosX` 15007744; tick 24 `PosX` 15167488, `PosY`
  11993088; `ForceAdjusted` 0 every tick; `SlideCount` 12.
- Mutation `sign_e`: tick 12 `PosY` **11567104** (176.5), then -32768 per tick.
- Binary: identical rows (no residue on this approach).

### TSL9 — the entity gate (`:595`)
- Montage: T-SL1's (`OneCell`, (263.0; 200.0), speed 312, Up) plus an obstacle entity over the same cell:
  `JumpHeroRig.Build(FlatCells.Create(cell: …0x40 at (10,10)…), probeFactory: host => new AlundraMovementObstacleProbe(host), configure:
  (world, host) => wall = ContactWorld.AddEntity(world, host, "Wall", 250, 167, 0, -10, -7, 0, 24, 16, 32), x: 263f, y: 200f, freePad: true)`
  then `Controller.Settings.WalkabilityMask = 0x40` (what the file's `Rig` does, `:46-52`). Helpers exist: `JumpHeroRig.Build`
  (`AlundraJumpTestSupport.cs:282-335`, `probeFactory`/`configure`), `ContactWorld.AddEntity` (`AlundraContactTestSupport.cs:129-177`; the box
  is set by `SetEntityDimensions`: x [240, 264), y [160, 176), z 0..32, `Collidable`, no program: `CheckEntityInteraction` returns 0,
  `AlundraPlayerManager.cs:688-709`). Either extend the private `Rig` with two optional parameters or build in the test.
- DLL values: ticks 1-8 = T-SL1's (`PosY` ... 11993088, `PosX` 17235968); `XCollisionEntity` is the wall from tick 8 (the probe is asked
  before the field, `CharacterControllerComponent.cs:1306-1325`; `XCollisionEntity = H2Obstacle ?? H1Obstacle`, `AlundraEntityScriptProxy.cs:2071-2078`);
  ticks 9-12 `PosX` 17235968, `PosY` 11993088, `ForceAdjusted` **1**, `SlideCount` 0.
- Mutation `gateent`: tick 9 `PosX` **17285120**, `SlideCount` 1, `ForceAdjusted` 0. Control (model): with the wall entity alone and no
  blocking cell, the mutation is invisible (no corner blocked -> null slide -> FA 1): the blocking cell is required.
- Binary: the gate is the binary's (plan H4-R2 "aucune entité n'est en contact", verifier "ordre des portes relus dans le binaire", :5835).

### TSL10 — the agreement rule (`:586`)
- Montage: `Rig((cx, _) => cx <= 9, 250f, 200f)` (hero's left edge exactly on the wall line x = 240), `SteadyWalk(rig, 16, -159744, 0)` (direction
  north, an X force left from a westward walk), Up held, 2 ticks. Tick 1: the recompute (`:451-464`) gives target (0, -106496), steps 79872 /
  53248, forces (-79872, -53248): the direction disagrees with the step.
- DLL values: tick 1 `PosX` 16384000, `PosY` 13053952, `ForceAdjusted` **1** (posed by the Move, X made no progress), `SlideCount` 0; tick 2
  `PosY` 12947456, `ForceAdjusted` 0.
- Mutation `accord`: tick 1 `ForceAdjusted` **0** (the cardinal branch looks at Y alone, which progressed).
- Binary: `ForceAdjusted` 1 at tick 1 (joint step blocked, north table, corner [2]); the binary's Y does not move (200.0) where the DLL's does
  (O-E19-28 a, accepted). The pinned FA is the binary's.

### Slide moved into `MoveControllerAndPullPosition` (`direct`)
- **Fact.** The only production callers of `MoveControllerAndPullPosition` are `AlundraScriptedMotion.cs:518` (the kinematic tick, which then
  calls `SlideAlongWall`) and `:663` (the slide's own Move). The mutation moves the call inside the method with a recursion guard: production
  behaviour is unchanged. Only direct test calls see it (`AlundraNpcCharacterControllerMoverTests.cs`, `AlundraMovementObstacleProbeTests.cs`),
  which use direction 0 or a disagreeing force, hence the survival. The E19.h2 sketch (plan :5664-5670) adds no direct caller.
- **Optional contract test TSL11** (pins plan H4-R2 "for every direct call, today's rule"): T-SL1 for 8 ticks (contact; `FinalForceX/Y` stay
  (0, -159744), `TargetDirection` 16), then `rig.Hero.MoveControllerAndPullPosition(0f, -159744 / 65536f)`: `PosX` 17235968, `SlideCount` 0,
  `ForceAdjusted` 1; under `direct`: `SlideCount` 1, `PosX` 17285120. Recommendation: close as production-equivalent; TSL11 only if the main
  session wants the contract pinned (P4).

### Oblique table term (`:605-613`)
- **Fact (by construction).** For an oblique direction the corners are sampled at the position the first Move reached (`BlockedCorners(entity, 0, 0)`,
  `:609`); the engine only accepts free positions under the same rules (`IsHorizontalMoveBlocked`, `CharacterControllerComponent.cs:1327-1362`: 4
  corners, walkability mask, ground above foot + step) and both far edges are exclusive by one unit, so `Corner(0) && Corner(3)` /
  `Corner(1) && Corner(2)` is false whenever it is evaluated; `adjusted` reduces to `forbidden || (noProgressX && noProgressY)`. Reaching it would
  need a divergence between `WalkabilityMaskFor(Flags)` / `PosZ + StepHeight` and the controller's live settings: a contrived montage.
- In the binary the term reads the corners of the last BLOCKED attempt (the convex tip, T-SL5 of `e19h-disc/B/notes.md` :160-161: FA and stop):
  that is stage 2, not ported (D-E19-58). **Recommendation: close, recording the reason** (no test can reach it without a production change).

### UO-1 through `0x42`
- **Fact.** UO-1 (`AlundraHeroSlideTests.cs:208-222`) runs `46 FF` on an entity that is its own logic entity, so the handlers' operand
  (`AlundraEventProgramRunner.cs:666-673`, `entity` = logic, passed at `:419-420`) is never distinguished from `owner`. UO-2 likewise
  (`RunOpcode(rig.Hero, …)`). `0x42` itself is tested by `AlundraEventProgramRunnerLogicEntityTests.cs:260-277`.
- **Test.** In `AlundraEventProgramRunnerLogicEntityTests.cs` (its `FakeWorld` already carries `PlayerEntity`, `:28-47`, no new context class):
  hero `{ IsPlayer = true, Status = Normal, Flags = 0x100 }`, `world.PlayerEntity = hero`, owner `Entity(1)`; program `42 46 FF` with
  `Result = 7` -> hero `Flags` 0x2100, owner `Flags` unchanged, `CodeIndex` 2, `Result` 7; then `45 FF` (same owner, its word still the hero)
  -> hero 0x100, `CodeIndex` 1. (Alternative in the slide file: `owner.LogicEntity = hero`, the pattern of `:69-84`; same kill, but not
  literally "through 0x42".)
- **Mutation.** `:668` and `:672` `entity.Flags` -> `owner.Flags` -> hero `Flags` 256 instead of 8448. Survives today (UO-1, UO-2: owner = logic).

## M-32, overlay half (`AlundraCellVisualSync.cs:153-161`)

- **Fact.** `_flatFloorCells` (`:78`, filled at `:153-161`) holds the cells with a floor id at load and NO overlay entry; `ProcessCellFloor`
  (`:236-327`) adopts a mutated floor unless `!hadExisting && _flatFloorCells.Contains(key)` (`:255`). Dropping
  `&& !sync._floorModel.ContainsKey((x, y))` (`:157`) puts the load-overlay cells into `_flatFloorCells`; it is visible only when such a cell is
  first emptied (entry removed, `:242-248`) and later refilled (`hadExisting` false): the refill becomes degraded (elevated) or silently
  skipped (height 0) instead of adopted.
- **Why the tests miss it (fact by reading).** The three R2 tests use `CreateEmptyAreaFixture` (`:743-776`) with `floorRecords: null`; no test of
  the file writes the same destination twice except `(1,3)` at `:871/:873`, in that placement-free fixture; the 389 tests do one copy per
  destination (`:291`, `:331`, `:496`, `:535`, `:576`, loop `:667-670` over four doors). No other test file observes the sync's overlay
  (`grep CellVisualSync|SortedOverlay` in `Alundra.Tests`: only this file, `AlundraWorldProxyGlobalFreezeTests.cs` comments, and
  `WallPlacementOverlayTests.cs`, which never builds a sync). The whole-class mutation run of the executor settles it.
- **Montage (helpers all exist).** A sibling of `CreateEmptyAreaFixture`: `CreateSyntheticComponent(2, 8)` (`:783-827`, every flat cell local 5,
  raw 60 -> local 5, raw 61 -> local 9); cells as the empty-area fixture plus `(1,7)`: raw 60, height 3; a `FloorPlacementRecords`
  (`WallPlacementOverlay.cs:49-60`) `{ MapIndex 1, Count 1, CellX [1], CellY [7], Plane [0], X [1], Y [4], Gid [6], DepthSlot [0] }`
  (`FirstGid` 1, `:87`); `submitted = WallPlacementOverlay.ApplyFloor(component, records, "map_476_like")` (`:306-345`, strips the flat tile
  at (1,4)); `AlundraCellVisualSync.Create(…, floorRecords: records, submittedFloorIndices: submitted, …)`; `store.CellsMutated += sync.OnCellsMutated`.
- **Test and values (exact).** Precondition: `submitted` = [0]; overlay single `(TileSetIndex 0, TileId 5, GridX 1, GridY 4)`, key
  `ComputeFloorSortKey(7, 0, 0)`. `CopyCellRectangle(0, 7, 1, 1, 1, 7)` + flush -> overlay empty. `CopyCellRectangle(0, 5, 1, 1, 1, 7)` + flush
  -> single `(0, 5, 1, 4)`, key `ComputeFloorSortKey(7, 0, 1)` (stable id = `records.Count` = 1, `:150`, `:282`; depth slot of raw 60 = 0,
  `:454-458`), and no "degraded" warning (`CapturingWarningLogger`, `:882-920`). A height-0 refill from `(0,6)` gives `(0, 9, 1, 7)`, key
  `ComputeFloorSortKey(7, 0, 1)`, as an optional second assertion.
- **Mutation.** `:157` without `&& !sync._floorModel.ContainsKey((x, y))` -> after the refill the overlay is empty and one "degraded" warning is
  logged (`Assert.Single` sees 0 entries).
- **Value.** P3, real data: the verifier counted 20 such cells adopted on the 476 (plan :6600). The warp-out-of-163 half stays G1-late (an arc).

## m1 advisory (`AlundraTurnOrderTests.cs`)

- **Fact.** `FakeEntityWorldContext` is a **private** class nested in `AlundraEventProgramRunnerTests` (`:38-91`, 85 uses in that file);
  three more private copies with the same name live in `AlundraDialogueFlagMarkerTests.cs:57`, `AlundraDialogueOpcodeDispatchTests.cs:70`,
  `AlundraDialogueYarnRenderingTests.cs:44`. `Alundra.Tests` holds **28 private `IEntityWorldContext` implementations in 27 files**
  (`grep "class .*: IEntityWorldContext"`). `AlundraTurnOrderTests.WorldContext` (`:24-36`) is a 12-line fake of the same shape.
- **Assessment.** Reuse would mean extracting a shared fake from a file of 85 uses (plus deciding about the three dialogue copies, territory of
  the dialogue tests), i.e. a test-support refactor outside a hygiene slice (author rule: no refactor beyond the task). No defect, no mutation.
  **Recommendation: close without change**, recording that the per-file private fake is the repo's convention.

## Ownership against pending work

- **D5b** (paused, plan :3745, :3861; F2: `EntitySupport.cs`, the object-top tests, `IsOnGround` rule of F2, UH-16): owns none of the files
  above except the SJ-12 ternary. `AlundraJumpTestSupport.cs` / `AlundraContactTestSupport.cs` are used read-only by TSL7-TSL10 (D1-D5 may add
  helpers there, plan :3959-3961). M-19c alone could be moved by D5b (entry tick) -> another reason to close it.
- **E19.h1b2 / E19.h2 sketches** (plan :5616-5670): production work on Z of entities without a controller and the NPC air state; no file of this
  slice is named; h2 adds no direct caller of `MoveControllerAndPullPosition`.
- **Loop slice** (§1.2w, :7836-7859): audio (extractor, `bgm.json`, engine music streaming, the DLL music player); `AlundraHeroSlideTests` sits in
  `AlundraMusicPlayerSingletonCollection` (`:18`) only because of its arc guard; no file overlap.
- **E19.f2b** (:4751-4756): the dialogue view (XAML); no file overlap.
- **E19.f2a**: closed (`d353136`); touched none of these files.

## Facts vs hypotheses (summary)

- Facts: every file:line above at `d353136`; the scope files unchanged since `bafbd5a`; the h4 mutation patterns unique at HEAD; the only
  production callers of `MoveControllerAndPullPosition`; the M-32 observers; the FakeEntityWorldContext layout; the model reproducing T-SL1/T-SL3.
- Hypotheses (to be measured first; a contradiction is a stop): the exact engine contacts on positive directions (152.0 for TSL7, 229.0 for TSL8),
  hence every new row of TSL7/TSL8; the ladder value -65536; the absence of any whole-suite kill for `:157` (static reading only).
