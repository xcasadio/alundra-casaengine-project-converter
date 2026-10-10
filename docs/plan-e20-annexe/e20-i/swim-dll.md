> Report returned by one agent of the read-only discovery for E20.i, swimming (night of 2026-10-10), kept verbatim as evidence. The plan holds the reconciled facts.

Things that change the plan

1. **The swim slice also has to port the exits back to land.**
   - FACT: The binary's land state table `0x80022D3C` sends 0x0F, 0x1D and 0x28 to `0x80031DD0`. That sets Idle if `IsOnGround` is non-zero, else 0x2D, then runs `TryUseItem` and ends the tick.
   - FACT: It sends 0x3A and 0x3B to `0x80031DB4`, which flips the direction (+0x10) and then does the same.
   - FACT: The DLL's `MovePlayer` leaves any state it doesn't know unchanged (comment at line 538 of `AlundraPlayerManager.cs` at HEAD).
   - INFERENCE: Without these exits, a hero leaving the water, falling off a ledge, or warping out while swimming stays in a swim pose on land. The warp case is real:
     - `AlundraWarpDirector.BeginDepartureFromChangeMapOpcode` records `player.TargetAnimationId` (line 411).
     - `AdoptPlayerPawn` restores it on arrival (`AlundraWorldProxy.cs:1941`).
   - The envelope row says only "branche de pente 4 + états", so the land exits are not in the row.

2. **The slope-4 branch must go before the weapon-effect trio, not into the state chain.**
   - FACT: The binary's slope switch sends slope 4 to `0x8003260C`. On entry it clears `0x80126FFC` (the charge counter) and `0x80127110` (the weapon record pointer) and calls `UpdatePlayerCarriedEntity(2)` (`0x8002F884`).
   - FACT: It then dispatches on the table `0x80022E7C` (0x50 entries) and ends the tick. It never reaches `UpdatePlayerWeaponEffect`, `UpdateWeaponStepProgression` or `UpdatePlayerCarriedEntity(0)`, which are at `0x80031D68`-`0x80031D78`.
   - FACT: At HEAD the DLL calls that trio at lines 324-326 for every slope, and its slope-4 guard sits inside the state chain (lines 483-494).
   - So the insertion point is right after the slope-6 gate (line 320). The guard and its supporting code are deleted:
     - the guard branch at lines 483-494;
     - the `DeepWaterSlope` constant (line 118);
     - `IsRunState` (lines 567-569);
     - the `&& player.Slope_18c != DeepWaterSlope` clause in the common tail (line 1247), which becomes dead code.
   - `UpdatePlayerCarriedEntity(player, 2)` becomes a named stub for E20.g, and the aura stays untouched until E20.f1b.

3. **The deep-water wall (O-E19-22) is not needed to finish E20.i, but it changes what the recipe has to look like.**
   - FACT: Every swim oracle trace uses boots 0x1B (level 2): `R9-deepwater-boots2`, `R9-allwater-*`, and `C1` in the c2 oracle. None reaches the wall.
   - FACT: The binary's swim branch never reads the boots level. Only the wall does (`0x80037504`): it blocks the hero when `(cell word & 0xE00) == 0x800` and boots level < 2.
   - Consequences of leaving the wall out:
     - A kit-level hero enters deep water freely today and will swim freely after E20.i. The original forbids this.
     - `R9-deepwater-noboots` (bounce at tick 87) stays unpinned.
   - If the wall is ported separately, the E20.i recipe needs a test save with boots 0x1B (see "Test means").
   - Design trap, for whoever ports the wall:
     - Adding bit `0x800` to the hero's `WalkabilityMask` would also block ladder cells. Ladders have ground property 12, so their cell word is `0xC00`, and `0xC00 & 0x800` is non-zero.
     - The rule is an equality on the 3-bit field, which a plain mask cannot express.
   - INFERENCE: A pseudo-bit computed inside `AlundraCellsCollisionField.TrySampleGround` would work with no engine change. The engine only forwards `_settings.WalkabilityMask` to the field (`CharacterControllerComponent.cs:1346` and `:1584`).
   - `BlockedCorners` in `AlundraScriptedMotion.cs` builds its own mask from `WalkabilityMaskFor(entity.Flags)`, so it would need the same change.

4. **The oracle has no dive, exit or fall-in trace.**
   - FACT: 0x28 never appears in any G9 trace. The dive needs a press edge (`pressed & 0xD0` = Square, Cross or Triangle) while in 0x0F or 0x1D.
   - FACT: `R9-allwater-start-T` has Triangle held from tick 1. The hero is in Idle at that tick, so it goes to 0x1D and never dives.
   - A new oracle group is needed for the dive, the exit to shore, and the fall into water from a ledge. The 0x28 duration (42 ticks, chained to 0x1D) has no binary-side measurement yet.

5. **0x3A is not entered by anything in the DLL.** The hurt entry that writes 0x3A on slope 4 (`0x80031C74`-`0x80031C80`) is E20.c2b's. E20.i can port the 0x3A/0x3B handlers and test them by setting the state directly. D-E20-23 says c2b leaves a brief-wound stand-in in the DLL; E20.i must replace it. That stand-in does not exist at HEAD, so its location is UNKNOWN.

---

## 1. How `Slope_18c` is computed at HEAD

- FACT: It is computed for the hero only. `RunHeroProbes()` is called from `TickMotion` inside `if (IsPlayer)` (`AlundraEntityScriptProxy.cs` ~1493-1496). `RunHeroProbes` runs `UpdateGroundSlope`, `UpdateVramFlags` and `UpdateFloorHeight` once per logic tick, after `AlundraPlayerManager.Tick`.
- FACT: The next tick's `MovePlayer` reads the value. This matches the binary's one-tick lag.
- FACT: Other entities keep `Slope_18c = 0`. The only other consumer is `PickEventTrigger`'s `DestroyOnSlidingSlope` check (`Slope_18c == 4`), which is therefore dead for NPCs.
- FACT: `UpdateGroundSlope` sets the slope to 0 if the entity lacks the `Gravity` flag or the world's collision field is not an `AlundraCellsCollisionField`.
- FACT: `MovePlayer` sets `Flags |= Gravity` at line 275. The climb clears it.
- FACT: The rule (`ProbeSlopeCorner`):
  - It samples four corners: `(PosX+ModX)>>16`, `(PosX+ModX+Width)>>16`, and the same for Y.
  - A corner qualifies only if its ground height, as `round(h*65536)`, equals `PosZ+ModZ` exactly. The `+1` of the binary is deliberately dropped (documented deviation).
  - Any non-qualifying corner makes the result 0. Otherwise the result is the minimum of `(gp<<8 & 0xE00)>>9`.
  - Slope 4 therefore means all four corners sit on cells with ground-property slope bits 100 at the hero's exact Z.
- FACT: All deep-water cells in the export are flat for the height function (`slope & 3 == 0`) and have heights in whole 16 px units, so the equality is reachable.
- INFERENCE: A hero that spawns in water gets `Slope_18c = 0` at its first `MovePlayer`, because adoption runs no probe (no call site found). `C1` in the c2 oracle shows the same for the binary (slope 0 at tick 0, 4 after tick 1). The `allwater` G9 traces start with slope 4, because that harness settles first. `RunMontage.Start` also settles. A fidelity test against `allwater` therefore passes in the montage but could differ by one tick in production. UNKNOWN whether production matters here.

## 2. The E20.d1 guard

- FACT: Branch at lines 483-494:
  - It runs only after the Idle/Moving, jump, and climb cases, and only for the run states 0x29, 0x03, 0x2A, 0x04, 0x3E and 0x1C.
  - It flips the direction for 0x3E and 0x1C, sets Idle, and ends the tick. Idle then walks at the next tick.
- FACT: In the common tail (line 1247), a held Triangle does not start the run on slope 4.
- FACT: Idle, Moving, jump and attack states at slope 4 are not touched by the guard. They go through the land tail. So today on deep water the hero:
  - walks at the Moving speed (208), not the swim speed;
  - swings the sword on Square (the tail begins with `PlayerTryAttack`);
  - jumps on Cross.
  - In the binary, every one of these states goes to 0x1D and `PlayerTryAttack` is unreachable; Square, Cross and Triangle edges dive instead.
- The class doc (lines 32, 54-66) and the doc on line 117 mention the guard or the unported swim and need a refresh.

## 3. Swim states and animations in the DLL

- FACT: No constants or handlers exist for 0x0F, 0x1D, 0x28, 0x3A or 0x3B anywhere in `Alundra/Scripts/`. The only `0x3A`/`0x3B` hits are event opcodes. Nothing in the tests references these as hero states.
- FACT: The exports exist in all four directions (`alundra-project/Entities/Alundra/bankalundra_0_anim{15,29,40,58,59}_*.anim2d`). Durations are the maximum keyframe time over all tracks, in 50 Hz ticks, identical in all four directions:

  | State | Animation id | Type | Ticks | End (`IdsvAnimDirs`) | AnimSet speed / accel / sfx |
  |---|---|---|---|---|---|
  | 0x0F swim | 15 | Once | 42 | Chain to 15 (self-chain) | 144 / 4 / 22 |
  | 0x1D float | 29 | Loop | 36 | Loop | 0 / 3 / 0 |
  | 0x28 dive | 40 | Once | 42 | Chain to 29 (0x1D) | 176 / 0 / 21 |
  | 0x3A hurt in water | 58 | Once | 4 | Chain to 59 (0x3B) | 256 / 64 / 23 |
  | 0x3B | 59 | Once | 8 | Hold | 0 / 68 / 0 |

  No `IsZForceApplied` impulse and no `Flags` bits on these five.
- FACT, checked against oracle traces:
  - 0x0F self-chain at tick 44 (it started at tick 2, +42), `b0=1`. This matches `R9-allwater-start-R`.
  - The 0x1D loop turn at tick 37 (started at 1, +36). This matches `R9-allwater-start-T`.
  - 0x3A at tick 2, 0x3B at 6 (+4), 0x1D at 15. The hold flag is seen at 14, so the hold lasts 8 ticks. This matches `C1`.
  - The 0x28 duration of 42 is not binary-checked (no trace has a dive).
- FACT: Everything around the states is already generic:
  - sound and Z impulse on switch (`GiveImpulse`);
  - Chain/Hold/Loop bridge (`OnAnimationFinished`), which increments `AnimCompleteCounter` on a Chain end;
  - force step from `AnimSets` through `TickPlayer`, which uses `TargetAnimationId`.
- INFERENCE (arithmetic on the code, not run): `R9-deepwater-boots2` after tick 89 gives 0x1D at tick 90, with ForceX = 258048 − (258048>>3) = 225792, then 0x0F with target 110592 and step 7200: 218592 at tick 91. That equals the oracle rows. So no per-state physics code is needed.
- FACT: The effects skeleton (`AlundraPlayerAnimationEffects.cs`):
  - 0x28 is in `IsWalkStepState`, so it will call the E20.e stub `PlayerWalkStepEffects` once forces are non-zero.
  - 0x0F and 0x1D are in no dispatch table.
  - This matches the oracle: swimming shows only `snd22` (0x0F), no fx.

## 4. Binary facts the port copies (decoded here with capstone; cross-checks the other reports)

- FACT: `0x8003260C`:
  1. Clears `0x80126FFC` and `0x80127110`.
  2. Calls `0x8002F884(2)`.
  3. Reads the state `[0x80127DB8]` (hero +0x88). If it is ≥ 0x50, jumps to `0x80032758` (calls `0x800350C0`, the item-in-use driver, then END).
  4. Otherwise dispatches through `0x80022E7C`.
- FACT: The swim table sends:
  - `0x80032690` (swim/float handler): 0x0F, 0x1D.
  - `0x80032658` (flip direction, then 0x1D, then `TryUseItem`): 0x1C, 0x31, 0x3E.
  - `0x80032680` (`TryUseItem` only): 0x28, 0x3A.
  - `0x800326FC`: 0x3B.
  - `0x80032758` (skip): 0x0D, 0x1A, 0x1B, 0x1E, 0x27, 0x37, 0x38, 0x3D, 0x43, 0x45, 0x47, 0x48, 0x4A, 0x4C, 0x4D. These are the same states the land table sends to `0x80032758`.
  - `0x80032674` (state = 0x1D, then `TryUseItem`): everything else, which includes Idle, Moving, the jump and air states, the attacks, 0x36 LoadingMap, 0x0E climbing and 0x39.
- FACT: `0x80032690`:
  - TargetDirection = dir (the pad direction, or the current one when invalid).
  - `TryUseItem` == 0 ends the tick.
  - `CheckEntityInteraction`: 2 gives 0x1D; any other non-zero value ends the tick.
  - On result 0: `pressed & 0xD0` gives 0x28; else a non-zero direction nibble gives 0x0F; else 0x1D. `0x80022D3C`'s land exits are described in the first section above.
- FACT: `0x800326FC` (0x3B): `TryUseItem` == 0 flips the direction (+0x10 & 0x1F). Otherwise, when `+0xAC == 1`, the state becomes 0x1D and the direction flips.
- FACT, deep-water wall (`0x80037488`-`0x80037558`): `mask = 0x40 | (ClassB ? 1) | (ClassA ? 0x1000)`. Boots level `[0x80127000] < 2` adds the test `(cell & 0xE00) == 0x800`. It lives in the hero-specific corner test.

## 5. How the mover treats deep-water cells

- FACT, DLL: `AlundraCellsCollisionField.TrySampleGround` computes `tileFlags = walkability | (gp<<8)` and `isWalkable = (tileFlags & mask) == 0`. The hero mask is `0x40` plus ClassB `0x01` plus ClassA `0x1000`.
- FACT: Deep cells have `gp` 8, 24, 136 or 152 and walkability 0. Only `gp` 24 and 152 carry the 0x1000 bit (0x1800 and 0x9800), which blocks only ClassA entities. So deep water is walkable for the hero. `GroundHeight` is the plain cell height × 16.
- FACT: The hero walks into deep water in the DLL today, with no boots test and no wall. `AlundraScriptedMotion` reads the boots level only for shallow water (`VramOR & 0x08`, line 500) and the x160 impulse (line 288). Deep cells give `VramOR = 0x800`, so no halving applies.
- FACT, data:
  - 54 map indices have slope-4 cells. Among the .tileMap files in `alundra-project/Maps` (483), 89 have any ground-property bit 0x8; the other 35 have only ladders. The 54 are:
    0, 3, 4, 5, 6, 7, 8, 12, 14, 15, 17, 18, 19, 20, 49, 70, 72, 73, 75, 77, 78, 79, 80, 81, 83, 85, 88, 90, 139, 140, 141, 146, 158, 306, 307, 314, 335, 352, 354, 379, 393, 407, 408, 410, 413, 416, 429, 430, 433, 435, 439, 441, 442, 479.
  - Map 416 has 399 slope-4 cells: 311 at height 1, 76 at height 4, 12 others. Maps 8 and 416 share this layout.
  - On 416, 11 deep cells carry the portal-floor bit 0x8000 (`gp` 152 and 136). A hero can swim into a portal.
  - On maps 77 and 78, 26 deep cells have walkability 4 (a hole bit).
  - Map 14 has an 81-cell pond at tiles x 40-47, y 43-59, height 0, shore height 1.
- FACT: The swim exits follow the existing mover rules. The oracle notes a 16 px rise blocks the hero, and the DLL mover rejects a rise beyond the step height. On 416 most shores are walls; the level-shore exits (deep height 1, shore height 1) work.

## 6. Boots level

- FACT: `BootsLevelOf(state)` (line 1030): 3 if item 0x1C is owned, else 2 for 0x1B, else 1 for 0x1A, else 0. The test inventory and the `b1b` montage inventory use item 0x1B.
- FACT: It is stored in `AlundraEquipmentRecords.BootsLevel` by `UpdateItemEffectState` step 1, and read on demand by `AlundraScriptedMotion.BootsLevel`.
- FACT: The swim branch does not read the boots level. Only the wall does.

## 7. Tests and traces that involve deep water

Tests that move (all in `Alundra.Tests/`):
- `AlundraHeroRunStatesTests.cs` (lines ~480-520):
  - `DeepWater_EveryStateOfTheRun_GoesToIdle_ThenTheTickEnds_...`: a Theory with 6 cases. The expected state becomes 0x1D, and the direction flip for 0x3E and 0x1C stays.
  - `DeepWater_TheTailDoesNotStartTheRun_...`: Idle on slope 4 with Triangle+Right now gives 0x1D, not Moving. The `dry` (slope 0) and `shallow` (slope 2) assertions are unchanged.
- `AlundraHeroRunTests.cs`:
  - `G9_OnDeepWater_TheRunGoesToIdleAtTheTickAfterTheSlope4_ThenTheHeroWalks` (uses `g9/R9-deepwater-boots2`; slope 4 first at tick 89; it asserts Idle at 90, Moving at 91 and passes today by comparing only up to `lastTick: slope4`). After E20.i it should compare the whole trace.
  - `G9_ATriangleOnDeepWater_DoesNotPrepareTheRun_TheHeroWalks` (uses `R9-allwater-start-T-R`).
  - The header comment at line 19.
- `AlundraTestSavesTests.cs` moves only if a preset is added (it asserts `AllPresetNames`).

Tests and traces that do not move:
- The other G9 tests: shallow, shallow with boots, ice, flat reference.
- The G1-G8 groups.
- The hero traces on map 389 (no slope-4 cell) and the intro traces.
- `AlundraGroundSlopeTests` and the ladder tests (slopes 6 and 0).
- `AlundraWorldProxyEventPassTests.DestroyOnSlidingSlope_...` (hand-sets slope 4 on an NPC).
- The arcs: A7 on map 416 (the hero stays locked on the beach, slope-3 cells at tile (41, 47)); `HitArena` and the c2a arcs on map 14 at tile (8, 6), far from the pond; `AlundraTarnMurggArcTests` on map 15 (no hero movement seen).

Oracle traces (`docs/plan-e20-annexe/e20-d/oracle/out/g9/`):
- `R9-deepwater-boots2`, `R9-allwater-start-R`, `R9-allwater-start-T-R`, `R9-allwater-start-T` (swim, float, switch to run).
- `R9-deepwater-noboots` (the wall; not replayed).
- `docs/plan-e20-annexe/e20-c2/oracle/out/C1.txt` (informational; the hurt in water: 0x3A at tick 2, 0x3B at 6, 0x1D at 15, direction 24→8→24, ForceX −196608 decaying).

## 8. Concrete gaps and files a port touches

Production:
- `Alundra/Scripts/AlundraPlayerManager.cs`:
  - constants for the five states;
  - the slope-4 branch after line 320 (swim table, handlers, state-0x1D default, dive, `UpdatePlayerCarriedEntity(2)` stub, the two clears, then `EndOfMovePlayer` and return);
  - land exits for 0x0F, 0x1D, 0x28, 0x3A, 0x3B;
  - removal of the guard, `IsRunState`, `DeepWaterSlope` and the tail clause;
  - doc refresh.
- `Alundra/Scripts/AlundraTestSaves.cs`: a preset such as `swim-m14` or a 416 one, with `ItemCounts` for boots (`2 * 0x1B + 1`). No such preset exists. The Preset record already carries `ItemCounts`, and the weapon presets already pass them.
- `docs/plan-e20-heros.md` (new E20.i section, recipe line of E20.d, Q-D2) and a new oracle folder for the new traces. Run the repo's oracle tools from a copy, not in place.
- Not needed unless the wall joins the slice: `AlundraCellsCollisionField.cs`, `AlundraScriptedMotion.cs` (`BlockedCorners`), `AlundraWorldProxy.cs` (the mask at adoption), and `UpdateItemEffectState` (mask refresh when boots change).
- UNKNOWN: whether a first-tick probe at adoption is wanted (section 1).

Test means:
- `AlundraJumpTestSupport.cs`, `HeroAttackClock.Table`: five entries with the lengths and end kinds above and the AnimSet values from section 3. This is a test-support file, not a production one.
- New oracle scenarios, with expected values written before the run: the dive from both swim states by each of the three edges (Circle must not dive), swim↔float with direction press and release, turning while swimming, shore exit to Idle, fall into water from a ledge (0x2D then 0x1D), Square held while entering (charge cleared), and a 0x28 duration check.
- New tests: replay of the swim oracle, branch tests for the table, the land exits, 0x3A and 0x3B set by hand, and the inverted guard tests.

Out of scope for E20.i:
- `DestroyOnSlidingSlope` for NPCs (their slope stays 0).
- Sounds and effects of entering water: the AnimSet sound plays on the switch (generic); the T2 dispatch (the 0x28 splash) and the landing effects are E20.e stubs.
- The aura (E20.f1b).
- The portal and hole cells in water on maps 416, 77, 78 (exist in the data; no test now).

---

Files read: `Alundra/Scripts/AlundraPlayerManager.cs`, `AlundraEntityScriptProxy.cs`, `AlundraTerrainProbe.cs`, `AlundraCellsCollisionField.cs`, `AlundraScriptedMotion.cs`, `AlundraFrameSyncPasses.cs`, `AlundraPlayerAnimationEffects.cs`, `AlundraTestSaves.cs` (all at HEAD via `git show`), plus `Alundra.Tests/AlundraHeroRunTests.cs`, `AlundraHeroRunStatesTests.cs`, `AlundraRunSupport.cs`, `AlundraJumpTestSupport.cs`, and the E20.c2/E20.d annex docs. Scratch scripts are in `C:/Users/casad/AppData/Local/Temp/claude/D--development-repo-alundra-casaengine-project-converter/b00d1a72-420d-4acc-ab34-0c25fbdad3a1/scratchpad/disc-e20i/swim-dll/` (`dec.py` for the binary, `anims2.py`, `ends.py`, `herosets.py`, `scancells.py`, `map416.py` for the data).
