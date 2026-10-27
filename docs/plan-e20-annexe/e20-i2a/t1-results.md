# E20.i2a - T1 results (2026-10-27, revision 4 of docs/plan-e20-heros.md section 1.14.2)

Two commits: the tests first (`f76193c6`, red before the code), then the code (this commit). Logs in `t1/` (`red-before.log`: the 43 tests of the new files and of `HeroSwimArenaTests` before the code, 29 red, 14 green; `release-p1a.log` to `release-p5.log`: the Release suite after the code).

## The tests (Release, copy neutralised, `--blame-hang-timeout 60s`)

| Test | Before the code | After |
|---|---|---|
| (c1) `DeepWaterWallFieldTests`: `TheBit_BlocksTheCellsOfTheDeepWater_...` (2 rows), `TheBit_HasNoConditionOnTheHeight` (3 rows) | red (5) | green |
| (c1) `DeepWaterWallFieldTests`: `TheBit_BlocksOnlyTheEqualityOnE00_...` (4 rows: ladder, shallow, 0xA00, none), `TheBit_DoesNotChangeTheOtherBitsOfTheMask` | green (the field ignores the bit: they pass before and are the guard of the `& 0x800 != 0` mutant) | green |
| (c1) `TheBitIsAboveSixteenBits` (production constant = 0x10000) | not in the first commit (it names the constant; added with the code) | green |
| (r1) `HeroDeepWaterWallTests.R1_...`: 11 exact traces, 3 east-wall traces (R9, W1-run-L0, W1-run-L1: shift from 85 by 1, tolerance 1), 2 W2 (frozen position), 2 W5 walks, W10, W5-L0-wall-diag (per-tick bounds of D-I2A-6 (t2) rev. 4) | red (20) | green, all 15 traces within the subset T0 (c) wrote for each |
| (r2) `R2_WithTheBootsOfLevel2Or3_...` (7 traces: R9-boots2, R9-allwater-start-T-R, W1-walk/run L2 and L3, W2-L3-R; mask without the bit) | green (witness) | green |
| (r3) `R3_AnOpcodeOfFlagsAimedAtTheHero_KeepsTheBit` | red | green |
| (r4) `HeroDeepWaterWallWorldTests.R4_TheAdoptionOfTheHero_...` (real portal arrival on map 14, with and without the boots) | red | green |
| (r5) `HeroSwimArenaTests.WithoutTheBoots_TheHeroStopsAtTheEdgeOfTheDeepWater_O_E19_22` (the former deviation test, inverted) | red | green |
| (r6) `R6_TheFunctionOfTheTick_LeavesTheGravityAndTheMaxFallSpeedOfTheHeroAlone_...` | red | green |
| (r7) `HeroDeepWaterWallWorldTests.R7_AnEntityThatIsNotTheHero_...` | green (witness) | green |

Suite after the code (Release): p1a 332, p1b 634, p1c 275, p2 1 330, p3 550, p4 2 208, p5 2 206 = **7 535 tests, 0 red** (7 489 of HEAD + 46 new: 11 field, 29 wall, 2 world, 1 preset content, 3 preset theories gaining a row).

## The tests that move (against the predicted list of T0 (c))

1. `HeroSwimArenaTests.WithoutTheBoots_TheHeroSwimsToo_AnAcceptedDeviation_O_E19_22` -> inverted (predicted), (r5).
2. `PrefabArcSupportTests.PrefabMode_OnMap478_TheHeroIsTheRealPrefabOfTheHero` -> line 126 pins `0x41 | DeepWaterWallMaskBit` (predicted since revision 3).
3. The preset list: `TheThirtyFourPresets_...` renamed `TheThirtyFivePresets_...` (one row, `mur-m14`), the three `EveryPresetName` theories gain a row, `TheOtherPresets_LeaveTheDeathCounterAtZero` runs over the longer list (predicted mechanical changes).

Deviation from the T0 inventory (not a moved test, a support pin): T0 wired only the function of the tick. The plan (D-I2A-2, r4) also gives the bit at the adoption of the pawn, so the hero carries it from its arrival, before its first tick. `ArcRun` (the constructor, line 261) asserted the live mask of the hero `== 0x41` after its resync: with the adoption it is `0x41 | DeepWaterWallMaskBit` while the hero has no boots of level 2, so that support pin is derived (`0x41 | wall`, `wall` read from `PlayerManager.BootsLevelOf`). Without that derivation every prefab arc stopped at the constructor (7 of the 248 tests of the filtered run, `HeroSwimArenaTests`, `PrefabArcSupportTests` and the two new world tests, failed there); with it none moves. No test of a story chain (389 to 392, 179, intro) moves; no test loops.

## The code

- `CellsCollisionField.DeepWaterWallMaskBit = 0x10000` and its test in `TrySampleGround`: `(walkabilityMask & bit) != 0 && (tileFlags & 0xE00) == 0x800` makes the cell not walkable.
- `PlayerManager.RefreshHeroWalkabilityMask(hero, state)`: sets or clears the bit by `BootsLevelOf(state) < 2`; called by `PlayerManager.Tick` before `ScriptedMotion.TickPlayer` and by `WorldProxy.AdoptPlayerPawn` right after the mask is derived from the flags; never calls `ResyncControllerFromFlags`.
- `EntityScriptProxy.ResyncControllerFromFlags` keeps the bit it finds; `ScriptedMotion.BlockedCorners` reads the live mask of the controller (identical to `WalkabilityMaskFor(Flags)` for every other entity: the controller's mask is derived from the flags at spawn and at each resync).
- `RunMontage.Start` poses the bit by the production function (no pseudo-bit by hand).
- The preset `mur-m14` (`new Preset("mur-m14", "test-mur-m14", 14, 40, 48, 1, [], [], [], [])`), its content test, `docs/test-saves.md`.

`python -B tools/dll-structure/check_names.py --rules`: 0 findings, 1 warning (`Alundra.Tests/UI/Shared/TextBoxGpu.cs:266`, pre-existing). The Debug build and the Debug suite are T2's (they come last).
