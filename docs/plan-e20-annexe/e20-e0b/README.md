# E20.e0b - the pseudo-random generator: e0b1 (the advance, once per tick), e0b2 (the loot roll at creation)

Annex of slice E20.e0b1 (`docs/plan-e20-heros.md`, section 1.17 head and 1.17.1, revisions 1 and 2, READY at the extra closing review). This file is written and committed BEFORE any code
of the slice (task T0). The measured values of the later tasks are added in the section "Results" at the end. e0b2 will add its own sections.

## What is proved (e0b1)

The real frame function `0x8002BAEC` of `ALUN_CD.EXE` (France), run on the MIPS machine of the E14/E20 oracles (the discovery probe `s7_frame.py`, byte copy): it ends with ONE step of the
generator word `[0x80098708]`, `s <- s * 0x7D2B89DD + 0xE06A02E7 (mod 2^32)`, at `0x8002BD0C-0x8002BD34`, after the sound frame, whatever the gate word `[0x800DC4B8]`, the inventory result
or `a0` are. The DLL gets the same step once per logical tick, after the tick of the effect reservoir (D-E0B-1), never for an image without tick and never while a warp departs (D-E0B-2).

## How to run (from the repository root)

- `python -B docs/plan-e20-annexe/e20-e0b/oracle/run_e0b1.py` writes `oracle/out/` (about 3 s; exit 0 = every check of `out/summary.txt` is PASS). Two runs give the same `out/manifest.txt`.
- `python -B docs/plan-e20-annexe/e20-e0b/oracle/mutants_e0b1.py` writes `oracle/out/mutations.txt` (about 10 s; exit 0 = every mutant CAUGHT).
- Needs Python 3 with capstone, the disc EXE named in `oracle/tools/lib.py` and the map JSONs of `data-extracted/data/` (read only). Nothing outside `oracle/out/` is written.
- `oracle/tools/` holds the seven tools the probe needs (`lib`, `emu`, `machine`, `mips`, `c1world`, `world`, `bank`), byte-identical to `docs/plan-e20-annexe/e20-e0a/oracle/tools` (and
  `lib`, `emu`, `machine`, `mips` to the discovery harness); `espawn.py` is the e0a one; `s7_frame.py` is the byte copy of the discovery probe. `oracle/checksums.txt` lists the SHA-1 of every file.

## Expected values (the prediction, written before the run)

1. **The six words of the advance** (`out/advance_words.json`, an independent model against the constants of the plan): `0 -> E06A02E7`, `FFFFFFFF -> 633E790A`, `80000000 -> 606A02E7`,
   `B017C93D -> 35E36190`, `12345678 -> B806E07F`, and four steps from `12345678` -> `AFBE9EAC`.
2. **One advance per call of the frame function**, on the ten rows of the probe (`out/frame_vectors.json`): the machine journals one `ADVANCE` event (the store at `0x8002BD34`) per call, the last operation
   of the call; with the gate `0x48` shut the call makes exactly one step of the word (only the list pass runs), with the gate open it makes four (three marker draws of the stubbed passes, then the advance).
3. **`a0` has no effect on the number of steps**: `a0 = 0` and `a0 = 1` give the same word on the same gate (`a0` only commands the clearing of `[0x800DC4C4]`, which the rows 5 to 7 observe).
4. The ten rows (picked from the 24 calls of the probe, whose `frame_trace.json` keeps the md5 `4943e0d29796bdee75e0a876f82b6b48` of the discovery): gate `00` and `48` with `a0` 0 and 1 (rows 1 to 4);
   Start pressed with `DisplayInventory` returning 1, returning 0 (the request word `[0x800DC4C4]` ends at 1) and the post-load call `a0 = 1` returning 0 (the request is cleared again, rows 5 to 7); gate `08`
   with the trigger refused (row 8); the advance alone from the seeds `00000000` and `B017C93D` under the gate `48` (rows 9 and 10). The predicted values are computed by `run_e0b1.py` from the formula
   (3 marker draws if the gate has no `0x48` bit, then one advance), never read from the machine.

(The plan says "ten rows" without naming them: the choice above is this annex's; the 24 calls of the probe are all kept in `out/frame_trace.json` and all 24 are used by the mutants.)

## Qualitative prediction of the DLL (from the discovery, to be compared in T3 with the measured list)

The advance moves the generator by one step per tick; every test that reads the stream at a point where the number of ticks since the seed matters can move. At risk: `AlundraShipCorridorArcTests` (A5, A5r: the
`0x0C` of the 220th image), the arcs with a real AI (Beannoids and Muruta: maps 14 outside the arena, 44, 115, 116, 117), the arc of map 167 (`0x5A` mode 4) and the arcs of maps with `0x0C` / `0x8C`;
`AlundraPrefabArcSupportTests.ArcRun_ResetsTheSharedRandomStream...` (its seed is read right after the construction). The figure "about a dozen tests" is the critic's estimate, not a measure. Not at risk:
the direct calls of `RunFrame` without a callback (70 calls in 15 files), the grouping tests (`AlundraHeroChargeArenaTests` with 2 and 3 ticks per image, `AlundraHeroAuraWorldTests`) and
`AlundraTickStructureTests`: the callback is optional and the grouping invariance is kept by the place of the call (per tick, after the reservoir). The measured predicted list (rule of T1) is copied below
before phase B.

## Starting state (T0 measures, 2026-10-15, base commit `dbceb5e`, quiet window: nothing else built or ran)

| Measure | Value |
|---|---|
| `Alundra.Tests` Release | 4 833 passed, 0 failed, 3 m 27 s |
| `Alundra.Tests` Debug | 4 833 passed, 0 failed, 3 m 44 s |
| The five traces (`git diff --ignore-cr-at-eol --exit-code`) | unchanged (EOL noise restored) |
| `alundra-project/Alundra.dll` lock | not locked at T0 |

## Oracle results (T0)

- `run_e0b1.py`: every check PASS (`out/summary.txt`, 22 lines): the md5 of `frame_trace.json` equals the discovery, 24 calls, the six words, the ten rows (each with one `ADVANCE` event, last operation, steps
  and word as predicted, request word as predicted), `a0` without effect, one advance on each of the 24 calls. Two runs give the same `out/manifest.txt`.
- `mutants_e0b1.py`: 20 one-word mutants of the binary (RAM patches, never saved; the old word is asserted at each address), all CAUGHT, none survived, no equivalence to argue (`out/mutations.txt`).

| Family | Mutants (address) | Caught by |
|---|---|---|
| constants | multiplier halves `0x8002BD0C`, `0x8002BD18`; increment halves `0x8002BD20`, `0x8002BD24` | the word after the advance (every row) |
| operation | `mult` removed or squared (`0x8002BD1C`), `addu` -> `subu` or product only (`0x8002BD2C`) | the word after the advance |
| polarity of the `beqz s3` | `beqz` -> `bnez` (`0x8002BD30`) | row 7 (the post-load call must clear the request) and rows 5/6 |
| store of the advance | removed or moved to a neighbour word (`0x8002BD34`); read from a neighbour word or another base (`0x8002BD14`, `0x8002BD10`) | the word (steps 0 or a wrong value) |
| clearing of `[0x800DC4C4]` | removed (`0x8002BD3C`) | row 7 |
| result of `DisplayInventory` | request store removed (`0x8002BD00`), `bnez` -> `beqz` (`0x8002BCF4`) | row 6 and rows 5/7 |
| gate test | mask `0x48` -> `0x40`, `0x08`, `0` (`0x8003B394`), polarity (`0x8003B398`) | the step counts of the gate rows (gate `08` and `40` among the 24 calls, rows 2 and 8) |

## Predicted list: the shadow run of T1 (phase A code `fcec5c6`, Release, 2026-10-15)

`ALUNDRA_RNG_SHADOW_LOG=<file> dotnet test Alundra.Tests/Alundra.Tests.csproj -c Release --no-build --blame-hang-timeout 300s`: 4 834 passed, 0 failed (the 4 833 of the start plus the inertness test, which is a no-op with the sink on). The whole file is versioned as `e0b1-shadow.txt` (888 lines, one per block of consecutive events of the same attributed test; 394 distinct attributed keys, constructors and `Dispose` included; 267 keys with virtual advances, 147145 virtual advances in all).

**Predicted list** (rule of T1: `divergent_draws + divergent_seed_reads + ambient_draws > 0`, counters summed over the lines of a key): **66 tests**. A test outside this list reads from the stream, in phase B, only values identical to those of phase A, so it cannot move because of the advance.

| Test | virtual advances | draws | divergent draws | divergent seed reads | epochs | ambient draws | first divergent draw |
|---|---|---|---|---|---|---|---|
| `AlundraRandomTests.Next_ReturnsOnlyTheLow32BitsOfTheWrappedSeed` | 0 | 1 | 0 | 0 | 0 | 1 | - |
| `AlundraRandomTests.Next_ReproducesTheOriginalsFirstThreeValues_HandComputedFromTheLcg` | 0 | 3 | 0 | 0 | 0 | 3 | - |
| `AlundraRandomTests.Reset_SetsTheSeedBackToTheOriginalConstant` | 0 | 1 | 0 | 0 | 1 | 1 | - |
| `AlundraRandomOpcodeTests.SetRandomCardinalDirection_0x0C_FiveDrawsGiveTheBinarysDirections_AndTheSeedAdvancesOnce` | 0 | 5 | 0 | 0 | 0 | 5 | - |
| `AlundraRandomBranchOpcodeTests.IfRandomAtLeast_0x8C_TheComparisonIsGreaterOrEqual` | 0 | 2 | 0 | 0 | 1 | 1 | - |
| `AlundraRandomBranchOpcodeTests.IfRandomAtLeast_0x8C_TwoCallsDrawTwiceFromTheSameStream` | 0 | 2 | 0 | 0 | 0 | 2 | - |
| `AlundraRandomBranchOpcodeTests.IfRandomAtLeast_0x8C_TheFirstDrawIs53_SoThreshold128GivesZero_AndTheSeedAdvancesOnce` | 0 | 1 | 0 | 0 | 0 | 1 | - |
| `AlundraNativeEffectTests.WithoutASoundPlayer_TheLifeAndTheEffectsAreDone` | 0 | 8 | 0 | 0 | 0 | 8 | - |
| `AlundraNativeEffectTests.TheVessel_ReadsTheHeroWhereItIsWhenTheScriptRuns_NotWhenTheStageApplies` | 0 | 8 | 0 | 0 | 0 | 8 | - |
| `AlundraNativeEffectTests.TheVessel_OnTheNewGameStats_Answers1_DoesNotSuspend_RaisesTheLife_PlaysTheSoundBeforeTheEffects_AndCreatesTwelveSlots` | 0 | 8 | 0 | 0 | 0 | 8 | - |
| `AlundraHeroSwimTests.W6_TheOtherWeaponTraces_AreTheOracle` | 0 | 4 | 0 | 0 | 0 | 4 | - |
| `AlundraHeroSwimTests.W6_TheSquareHeld_ChargesOnTheLand_ZeroInTheWater_AndOnTheLandAgain` | 0 | 36 | 0 | 0 | 0 | 36 | - |
| `AlundraHeroRunTests.G7_TheSacredSword_CountsTheChargeInEveryStateOfTheRun` | 0 | 42 | 0 | 0 | 0 | 42 | - |
| `AlundraHeroAuraTests.AHeldCharge_RenewsTheAuraEvery97Ticks_AtTheTicks33_130_227_InTheSlotsOfTheOracle_OneAliveAfterEachUpdate` | 0 | 108 | 0 | 0 | 1 | 108 | - |
| `AlundraWeaponLockOpcodesTests.TheWholeProgramB_OfTheTwoMaps_HasNoSkippedOpcode` | 0 | 2 | 2 | 0 | 0 | 2 | opcode:AlundraEventProgramRunner.Dispatch |
| `AlundraStoredHitBoxTests.TheWorldsTick_StoresTheBoxOfAnEntityWithNoAttack_AfterThePhysics` | 11 | 9 | 9 | 0 | 2 | 0 | effect:AlundraNativeEffects.CreateHitSparks |
| `AlundraShipCorridorArcTests.A5r_HoldingDownInPlace_TheShipRollsTheHeroOnce_ThenTheDownPushedPortalLeadsToMap391` | 238 | 1 | 1 | 0 | 2 | 0 | opcode:AlundraEventProgramRunner.Dispatch |
| `AlundraPrefabArcSupportTests.ArcRun_ResetsTheSharedRandomStream_AtConstructionAndAtDispose` | 0 | 4 | 0 | 0 | 2 | 3 | - |
| `AlundraMurutaHallArcTests.H115_TheFourMuruta_DieByTheSword_TheDoorOfTheHallOpens_WithAFrameBreakEffectEach` | 428 | 106 | 106 | 0 | 2 | 0 | ai:AlundraEnemyHelpers.Below |
| `AlundraLeverPillarArcTests.D_Map117_TheLever22_StartsThePillar_AtTheTicksOfItsPrograms` | 107 | 9 | 9 | 0 | 2 | 0 | effect:AlundraNativeEffects.CreateHitSparks |
| `AlundraLeverDoorsArcTests.E_Map44_TheLever7_TurnsTheSixLevers_WalksTheSevenDoors_AndGivesTheHandBack` | 110 | 9 | 9 | 0 | 2 | 0 | effect:AlundraNativeEffects.CreateHitSparks |
| `AlundraLeverDoorsArcTests.E_Map44_WithoutG1633_TheHitOnlyRunsTheTestAndTheEnd_NoLockNoT80` | 48 | 9 | 9 | 0 | 2 | 0 | effect:AlundraNativeEffects.CreateHitSparks |
| `AlundraLeverBridgeArcTests.B_Map116_TheTwoLeversExtendTheBridge_AndTheSecondHitOnTheLever40RetractsIt` | 143 | 27 | 27 | 0 | 2 | 0 | effect:AlundraNativeEffects.CreateHitSparks |
| `AlundraLeverBasculeArcTests.C_Map117_TheBascule_RewritesThePlatformsAtTheTicksOfItsProgram_InFourHits` | 136 | 36 | 36 | 0 | 2 | 0 | effect:AlundraNativeEffects.CreateHitSparks |
| `AlundraLeverArcTests.A_Map115_TheNorthWestLever_TurnsAndOpensTheDoorAtTheTicksOfItsPrograms` | 105 | 9 | 9 | 0 | 2 | 0 | effect:AlundraNativeEffects.CreateHitSparks |
| `AlundraHitPassTests.H1_TheBasicSword_HitsABeannoid40PixelsAwayAtTick5_ThreeSparks_ThenTheCooldown_ThenD` | 50 | 9 | 9 | 0 | 2 | 0 | effect:AlundraNativeEffects.CreateHitSparks |
| `AlundraHitPassTests.TheReachInY_AtTheEdgesOfTheOracleWindows_IsTheOracleOne` | 480 | 81 | 81 | 0 | 30 | 0 | effect:AlundraNativeEffects.CreateHitSparks |
| `AlundraHitPassTests.TheReachInX_AtDyZero_IsTheOracleOne_FromMinus9To59Pixels` | 384 | 144 | 144 | 0 | 24 | 0 | effect:AlundraNativeEffects.CreateHitSparks |
| `AlundraHitPassTests.H1_TheThreeSparksOfTheHitExistOnTheReservoir_AtTheCentreOfTheOverlap_Effect9` | 15 | 9 | 9 | 0 | 2 | 0 | effect:AlundraNativeEffects.CreateHitSparks |
| `AlundraHitPassTests.TheReachBehindTheHero_Beyond9Pixels_IsOnlyContactedByTheLateFrames_BecauseTheHeroIsNotHurtYet` | 32 | 27 | 27 | 0 | 2 | 0 | effect:AlundraNativeEffects.CreateHitSparks |
| `AlundraHitPassTests.H5_TwoTargetsInTheReach_AreBothHitAtTheSameTick_TheHeroCountsTwoHits` | 50 | 18 | 18 | 0 | 2 | 0 | effect:AlundraNativeEffects.CreateHitSparks |
| `AlundraHitPassTests.H1near_TheBoxOfTheFirstFrame_OfTheSwingThatStartsAtThisTick_HitsOnThePressTick_TheLatchIsThe0x10OfTheTick` | 50 | 9 | 9 | 0 | 2 | 0 | effect:AlundraNativeEffects.CreateHitSparks |
| `AlundraHitPassTests.H1both_TheSwingAndTheBodyOfABeannoid20PixelsAway_HitEachOtherAtTick1_SixSparks` | 50 | 36 | 36 | 0 | 2 | 0 | effect:AlundraNativeEffects.CreateHitSparks |
| `AlundraHitPassTests.H7_TwoAttackersOnOneTarget_OnlyTheFirstOfTheListTouchesIt_TheSecondHitsItWhenTheCooldownIsOver` | 50 | 27 | 27 | 0 | 2 | 0 | effect:AlundraNativeEffects.CreateHitSparks |
| `AlundraHitPassTests.WhileTheContactLasts_ABeannoidTouchesTheHero_EveryTwentyFiveTicks_AndHisDamagedCounterStaysAtZero` | 90 | 45 | 45 | 0 | 2 | 0 | effect:AlundraNativeEffects.CreateHitSparks |
| `AlundraHitPassTests.H8_TheSwingToTheWest_HitsABeannoid40PixelsAwayAtTick5` | 50 | 9 | 9 | 0 | 2 | 0 | effect:AlundraNativeEffects.CreateHitSparks |
| `AlundraHitPassTests.H4a_ASecondSwing_HitsAgain25TicksAfterTheFirstHit_NotBefore` | 70 | 18 | 18 | 0 | 2 | 0 | effect:AlundraNativeEffects.CreateHitSparks |
| `AlundraHitPassTests.H6_AnImmuneTarget_GetsTheCooldownAndSparks_ButNoTouchingEntityAndNoProgramD` | 50 | 9 | 9 | 0 | 2 | 0 | effect:AlundraNativeEffects.CreateHitSparks |
| `AlundraHitPassTests.H3_TheHeroWalkingIntoABeannoid_IsTouchedAtTick17_TheCountersClearItAtTick18_TheHeroDoesNotLoseLifeYet` | 55 | 9 | 9 | 0 | 2 | 0 | effect:AlundraNativeEffects.CreateHitSparks |
| `AlundraHitPassTests.ARealArcOnMap14_TheHeroStrikesTheBeannoidOfRecord0_WhereTheMapPutItOnItsHill_SparksATouchAndTheProgramDAtTheNextTick_NoWound` | 22 | 9 | 9 | 0 | 2 | 0 | effect:AlundraNativeEffects.CreateHitSparks |
| `AlundraHitPassTests.H1edge_ABeannoid59PixelsAway_IsHitAtTick5_ByTheLastPixelOfTheReach` | 50 | 9 | 9 | 0 | 2 | 0 | effect:AlundraNativeEffects.CreateHitSparks |
| `AlundraHitPassCatchUpTests.ATargetThatLeavesAnAnimationWithTheByte0x40_AtTheTickOfTheFirstOverlap_IsHit` | 50 | 9 | 9 | 0 | 2 | 0 | effect:AlundraNativeEffects.CreateHitSparks |
| `AlundraHitPassCatchUpTests.TheBoxCursor_FollowsTheSpritesLogicalClock_AfterEveryFrame_WhateverTheNumberOfTicks` | 43 | 9 | 9 | 0 | 2 | 0 | effect:AlundraNativeEffects.CreateHitSparks |
| `AlundraHitPassCatchUpTests.ATargetThatEntersAnAnimationWithTheByte0x40_AtTheTickOfTheFirstOverlap_IsNotHit` | 100 | 9 | 9 | 0 | 4 | 0 | effect:AlundraNativeEffects.CreateHitSparks |
| `AlundraHitPassCatchUpTests.H1near_WithTwoTicksPerFrame_AfterAFrameOfOneTick_IsTheOracleTickByTick` | 50 | 9 | 9 | 0 | 2 | 0 | effect:AlundraNativeEffects.CreateHitSparks |
| `AlundraHitPassCatchUpTests.H1_WithThreeAndFourTicksPerFrame_IsTheOracleTickByTick` | 150 | 27 | 27 | 0 | 6 | 0 | effect:AlundraNativeEffects.CreateHitSparks |
| `AlundraHitPassCatchUpTests.H1_WithTwoTicksPerFrame_AfterAFrameOfOneTick_IsTheOracleTickByTick` | 50 | 9 | 9 | 0 | 2 | 0 | effect:AlundraNativeEffects.CreateHitSparks |
| `AlundraHitPassCatchUpTests.H1_WithTwoTicksPerFrame_ThePressOnTheFirstTickOfAFrame_IsTheOracleTickByTick` | 50 | 9 | 9 | 0 | 2 | 0 | effect:AlundraNativeEffects.CreateHitSparks |
| `AlundraHitPassCatchUpTests.H1near_WithTwoTicksPerFrame_ThePressOnTheFirstTickOfAFrame_IsTheOracleTickByTick` | 50 | 9 | 9 | 0 | 2 | 0 | effect:AlundraNativeEffects.CreateHitSparks |
| `AlundraHitPassCatchUpTests.TheHitWrittenAtTickN_IsReadByTheProgramsOfTickNPlus1_AndClearedByTheCounters_InTheSameFrameOfTwoTicks` | 50 | 9 | 9 | 0 | 2 | 0 | effect:AlundraNativeEffects.CreateHitSparks |
| `AlundraHitPassCatchUpTests.TheSameByte0x40Test_InAFrameOfTwoTicks_StillSeesTheSwitchOfTheTickItself` | 100 | 9 | 9 | 0 | 4 | 0 | effect:AlundraNativeEffects.CreateHitSparks |
| `AlundraHitPassCatchUpTests.ASwitchAskedAfterTheClockOfATick_IsTheSwitchTickOfTheNextOne_TheByteIsLoadedThereBeforeTheHitPass` | 100 | 9 | 9 | 0 | 4 | 0 | effect:AlundraNativeEffects.CreateHitSparks |
| `AlundraHeroRunArenaTests.G7_RunningIntoABeannoid_OnTheRealWorld_IsTheOracle_TheBodyTouchesAtTick60` | 70 | 9 | 9 | 0 | 2 | 0 | effect:AlundraNativeEffects.CreateHitSparks |
| `AlundraHeroRunArenaTests.G7_TheSweepOfThePressTick_OnTheRealWorld_IsTheOracleUntilTheHurt` | 1366 | 270 | 270 | 0 | 34 | 0 | effect:AlundraNativeEffects.CreateHitSparks |
| `AlundraHeroRunArenaTests.G7_TheTackle_OnTheRealWorld_HitsTheBeannoidAtTick61_ThreeSparks_ComparedUntilTheHurt` | 74 | 18 | 18 | 0 | 2 | 0 | effect:AlundraNativeEffects.CreateHitSparks |
| `AlundraHeroChargeArenaTests.TheChargedSwing0x18_OfTheSteelSword_TouchesABeannoidOfMap14` | 150 | 24 | 24 | 0 | 2 | 0 | effect:AlundraNativeEffects.CreateHitSparks |
| `AlundraHeroChargeArenaTests.TheReservoirAfterTheChargeAndTheRelease_DoesNotDependOnTheNumberOfTicksPerFrame` | 424 | 24 | 24 | 4 | 12 | 0 | effect:AlundraNativeEffects.CreateChargeSpark |
| `AlundraHeroChargeArenaTests.TheEffectsOfTheCharge_AreCreatedInTheWorldsReservoir_TheBurstAtTheRelease` | 105 | 6 | 6 | 0 | 2 | 0 | effect:AlundraNativeEffects.CreateChargeSpark |
| `AlundraHeroAuraWorldTests.AFrameHoldingTheTicks96And97AfterACreation_ShowsOneFrameWithoutAura_TheNextOneHasIt` | 182 | 46 | 46 | 0 | 3 | 0 | effect:AlundraNativeEffects.CreateChargeSpark |
| `AlundraHeroAuraWorldTests.TheHeroAnchor_ExposesTheKeyOfTheSortDriver_OnTheTicksOfTheSwingAndOfTheCharge` | 100 | 4 | 4 | 0 | 2 | 0 | effect:AlundraNativeEffects.CreateChargeSpark |
| `AlundraHeroAuraWorldTests.TheRenewalInFramesOfThreeTicks_WhereASparkTakesTheFreedSlot_StillGivesALiveAura` | 144 | 26 | 26 | 0 | 3 | 0 | effect:AlundraNativeEffects.CreateChargeSpark |
| `AlundraHeightTargetSitesTests.TheWaitOfTheBinaryEndsAtTheCallOfTheBinaryAtEveryRealSite` | 15547 | 27 | 27 | 0 | 66 | 0 | effect:AlundraNativeEffects.CreateHitSparks |
| `AlundraFlailBounceArcTests.AFlailAtTheHammerPost_BouncesFor16Ticks_AndTheRelationsOfTheBinaryHold` | 244 | 36 | 36 | 0 | 8 | 0 | effect:AlundraNativeEffects.CreateHitSparks |
| `AlundraFlailBounceArcTests.AFlailThatTouchesABeannoid40PixelsAway_BouncesAtTheNextTick_For16Ticks_ThenIdle_WithNoForce` | 100 | 18 | 18 | 0 | 4 | 0 | effect:AlundraNativeEffects.CreateHitSparks |
| `AlundraFireSwitchRegressionTests.ASwordHitOnAFireSwitchOfMap303_DoesNotLightIt_WhateverTheSharedResultHolds` | 82 | 18 | 18 | 0 | 4 | 0 | effect:AlundraNativeEffects.CreateHitSparks |
| `AlundraBeannoideGrateArcTests.G116_TheTwoBeannoids_DieByTheSword_AndTheGrateOpens_WithAFrameBreakEffectEach` | 312 | 90 | 90 | 0 | 2 | 0 | effect:AlundraNativeEffects.CreateHitSparks |

### The tests marked `ambient_draws` (T1 handling: written justification, no seed assignment added, so the shadow was not rerun)

Sixteen tests of the list have `ambient_draws > 0` (draws attributed to the test before any epoch of THAT test). None needs an explicit seed assignment:

- **The classes that reset the stream in their constructor** (the constructor has its own key in the attribution, so its `Reset()` is an epoch of `Class..ctor`, not of the method, hence the counter): `AlundraRandomTests`
  (3 tests), `AlundraRandomOpcodeTests` (1), `AlundraRandomBranchOpcodeTests` (3), `AlundraNativeEffectTests` (3). Their start state is the initial seed whatever the order of the tests: nothing is inherited, the counter is the artefact of the rule.
- **Montages of the hero without a world `Update`** (`AlundraHeroSwimTests.W6_...` x2, `AlundraHeroRunTests.G7_TheSacredSword...`, `AlundraHeroAuraTests.AHeldCharge...`): no virtual advance, the charge sparks draw their jitter from the inherited state. No assertion reads a spark
  (the assertions read the states, the counters, the forces of the hero and, for the aura, the aura slots), so the inherited state cannot reach one. `AlundraHeroAuraTests` reads the seed in a field initialiser and writes it back at `Dispose`: an epoch, no assertion.
- `AlundraWeaponLockOpcodesTests.TheWholeProgramB_...`: the random opcodes of two whole programs draw from the inherited state; the only assertion is "no skipped opcode".
- `AlundraPrefabArcSupportTests.ArcRun_ResetsTheSharedRandomStream...`: its three `Next()` before the arc are the ambient draws; the arc then resets the stream (an epoch) and the two seed reads follow it (zero divergent seed read in the shadow).

A test of the list that does not move in phase B is noted in T3 with the reason (the divergence reaches no assertion).

## Results

### Phase A (T1, code commit `fcec5c6`)

| Measure | Value |
|---|---|
| `Alundra.Tests` Release | 4 834 passed, 0 failed (4 833 + the inertness test), 3 m 37 s |
| `Alundra.Tests` Debug | 4 834 passed, 0 failed, 3 m 51 s |
| `cmp alundra-project/Alundra.dll Alundra/bin/Debug/net9.0-windows/Alundra.dll` | identical (the DLL was not locked) |
| The five traces | unchanged (`git diff --ignore-cr-at-eol --exit-code`) |

No test moved in phase A: the callback only feeds the virtual word of the sink.

### T2 - `AlundraRandomPerTickTests` on the phase A code (`fa1c15c`), run before any phase B code

The class is split in two files (`partial`): `Alundra.Tests/AlundraRandomPerTickTests.cs` (b, e: green by construction, committed with phase A) and `Alundra.Tests/AlundraRandomPerTickTests.Advance.cs` (a, c, d: red, kept out of the
commit until phase B). Phase A suite with the committed part: Release 4 837 passed, Debug 4 837 passed (4 833 + the inertness test + the 3 tests of (b) and (e)); `cmp` of the DLL identical; the five traces unchanged.

| Group | Test | Phase A result | Reason |
|---|---|---|---|
| (a) | `AdvanceFrame_StepsTheStreamOncePerCall_ToTheWordsOfTheBinary` x6 (the six words) | 6 RED | the real state does not move |
| (c) | `AWorld_StepsTheStreamOncePerTick_OneTickThenTwo` | RED | 1 tick = 1 step, then 2 ticks = 2 steps |
| (c) | `AWorld_UnderTheMenuGate0x48_StillStepsOncePerTick_...` | RED | gate `MenuOpen`: 1 step per tick |
| (c) | `TheFirstFrameOfAWorld_CountsAtLeastOneTick_EvenWithNoElapsedTime` | RED | the floor of the first frame: 1 step, then none |
| (c) | `TwoWorldsInARow_StepTheStreamOncePerTickEach_...` | RED | 2 steps then 1 step of the second world, 3 in all |
| (c) | `AFrameWithoutTick_ThenAFrameOfOneTick_MakeExactlyOneStepInAll` | RED | exactly 1 step in all (a positive case in the same assertion) |
| (c) | `AFrameThatDepartsToAWarp_BetweenTwoFramesOfOneTick_MakeExactlyTwoStepsInAll` | RED | exactly 2 steps in all; the departure frames are checked at 1 step |
| (d) | `TheChargeSparks_DrawBeforeTheAdvanceOfTheirTick_WhateverTheGrouping` x3 (1, 2, 3 ticks per frame) | 3 RED | the word after each spark and at the end |
| (b) | `RunFrame_CallsTheCallbackOncePerTick_AfterTheReservoirTick_AlsoOnAFrozenTick_AndNeverForZeroTicks` | GREEN | pins the phase A wiring |
| (b) | `RunFrame_CallsTheCallbackAfterTheQueuedOperationsAndTheUpdateOfTheirTick` | GREEN | pins the phase A wiring |
| (e) | `RunFrame_WithoutACallback_AdvancesNothing` | GREEN | the direct calls stay inert |

15 red, 3 green, as the slice predicts. As a check that the red tests can be satisfied (not a task of phase B, nothing committed, the file restored with `git checkout`): with the one phase B line added locally (`AdvanceFrame()` also writing
the real state) all 18 tests were green.

**Deviation from the plan text (numbers only).** The plan (T2 (d)) says "the charge spark of ticks 85 and 89" and "4 draws". On the arena of `AlundraHeroChargeArenaTests` (the real world of map 14, steel sword, press at tick 1) the hold
sparks are created at ticks **83, 87 and 91** (91 is the release tick, which also creates the burst of 16 effects, no number drawn): 3 sparks, 6 draws, 19 creations in all (the numbers 85 and 89 belong to the montage of `AlundraHeroChargeTests`).
The test pins the measured ticks (readable at one tick per frame) and derives the words from them; the principle (the spark draws before the advance of its tick, whatever the grouping) is the plan's.

(T3 and T4 add their measures below.)

### Phase B (T3, code commit `3f00274`) and T4

Phase B is the one line `_seed = _seed * 0x7d2b89dd + 0xe06a02e7;` in `AlundraRandom.AdvanceFrame()` (the sink still receives `OnVirtualAdvance()`, so `V = R` from now on), the comment of `RunActiveEffectsPass` and the header of `AlundraHitPassTests`.

| Measure | Value |
|---|---|
| `Alundra.Tests` Release | 4 852 passed, 0 failed (4 837 + the 15 red tests of T2, now green), 3 m 47 s |
| `Alundra.Tests` Debug | 4 852 passed, 0 failed, 3 m 48 s |
| `cmp alundra-project/Alundra.dll Alundra/bin/Debug/net9.0-windows/Alundra.dll` | identical (the DLL was not locked) |
| The five traces | unchanged (`git diff --ignore-cr-at-eol --exit-code`; the CR/LF-only noise of four files restored with `git checkout`) |
| T2 (a), (c), (d) | 15 red before phase B (table above), 15 green after |
| Existing tests that moved | **0** of the 66 predicted; 0 outside the list; no grouping, structure or direct `RunFrame` test moved |

**The 66 predicted tests did not move** (the divergence reaches no assertion). Reasons by family, read from the tests (not re-measured assertion by assertion):

- `AlundraRandomTests`, `AlundraRandomOpcodeTests`, `AlundraRandomBranchOpcodeTests`, `AlundraNativeEffectTests` (ambient only): no world `Update`, hence no advance; they reset the stream in their constructor.
- The hero montages without a world `Update`, `AlundraWeaponLockOpcodesTests`, `AlundraPrefabArcSupportTests.ArcRun_ResetsTheSharedRandomStream...`: see the justifications above; no advance or an epoch (the arc re-seeds) before any read.
- The hit-pass suites (`AlundraHitPassTests`, `AlundraHitPassCatchUpTests`, `AlundraHeroRunArenaTests`, `AlundraHeroAuraWorldTests`, `AlundraFlailBounceArcTests`, `AlundraFireSwitchRegressionTests`, `AlundraHeightTargetSitesTests`, `AlundraStoredHitBoxTests`, the lever arcs): the speeds of the sparks come from the stream and are not compared (header of `AlundraHitPassTests`).
- `AlundraHeroChargeArenaTests` (3 tests): the assertions compare the reservoir between groupings and the effect counts; `RunCharge` sets the seed after the construction of the arena and the two groupings advance identically, so the compared words agree (the one that reads the seed, `TheReservoirAfterTheChargeAndTheRelease_...`, compares runs of the same code).
- `AlundraShipCorridorArcTests.A5r_...`: the discovery predicted a move (note 6); the pin `DirectionAfter120 = 0x02` is read before the roll of the 220th frame and the later asserted outputs (hero positions at the end of frames 220 to 225, the portal) did not change. The plan's remark (the pin survives by coincidence for 219 to 221 advances) holds; no pin was rewritten.
- `AlundraMurutaHallArcTests.H115`, `AlundraBeannoideGrateArcTests.G116`: the AI draws differ, the asserted outcomes (deaths, the door opening, the break effects) do not depend on them.

Since nothing moved, no pin needed a derivation (D-E0B-8 applies to the new tests, which compute their words by a local step).

**DLL mutants of T4** (`mutants_dll.py`, applied to the working-tree file, build with the neutralised copy, run on the T2 class, `AlundraTickStructure`, `AlundraHeroAuraWorld`, `AlundraHeroChargeArena`, `AlundraHeroChargeTests`, `AlundraDestroyEntityBreakEffect` and `AlundraNativeEffectTests` (190 tests), restored by `git checkout`, `git status` clean after):

| Mutant | Result |
|---|---|
| M1 callback not passed by the world | CAUGHT, 9 red (the world tests of (c) and (d)) |
| M2 callback before the reservoir tick | CAUGHT, 2 red (both (b) tests) |
| M3 callback skipped on a frozen tick | CAUGHT, 2 red ((b) frozen tick and the gate `0x48` world test) |
| M4 callback once per frame instead of once per tick | CAUGHT, 8 red (grouping test of the charge arena at 2 and 3 ticks, (b), (c), (d)) |
| M5 callback also for a frame without tick | CAUGHT, 4 red (frame without tick then one tick; warp departure; (b); first frame) |
| M6 `IsTransitionInProgress` inverted | CAUGHT, 12 red |
| M7 `IsTransitionInProgress` ignored | CAUGHT, 1 red (the warp-departure case between two frames) |
| M8 first-frame floor removed | CAUGHT, 1 red (`TheFirstFrameOfAWorld_...`) |
| M9 multiplier constant changed | CAUGHT, 14 red |
| M10 addend constant changed | CAUGHT, 15 red |
| M11 `AdvanceFrame()` does not write the real state (phase A) | CAUGHT, 15 red |
| M12 callback called twice per tick | CAUGHT, 11 red |

Twelve mutants, no survivor. The two fresh verifiers of T4 (one on the pins: predicted list against measured list) are run by the main session.

---

# E20.e0b2 - the loot roll at creation (T0: prediction and oracle)

Annex of slice E20.e0b2 (`docs/plan-e20-heros.md`, section 1.17 head and 1.17.2, revisions 1 and 2 and the three closing reviews, READY on 2026-10-16). This part is written and committed BEFORE any code of the slice
(task T0). The e0b1 sections above are unchanged.

## What is proved (e0b2)

The real creation routine of `ALUN_CD.EXE` (France), run on the MIPS machine: `InitializeEntity` ends with `0x80032A40`, which stores the rolled item at `+0x3C` and the game flag at `+0x40`, through
`ChooseRandomlyAnItem` `0x80032968` (the chain over the table `0x80028E0C`, octet `0x960C` of the file, 2 048 bytes, md5 `7c6b97ac50de72f436a6fbf08ed6bcd7`). The DLL gets the same roll, once per creation, as the last
statement of the `try` of each of its three creation sites (D-E0B-6), in a pure class `AlundraLootRoll` (D-E0B-4). Nothing falls (the drop, its veto and the pickable object are E20.e0c).

## How to run (from the repository root)

- `python -B docs/plan-e20-annexe/e20-e0b/oracle/run_e0b2.py` runs every probe below in order and writes `oracle/out/` (about 2 min; `--quick` skips the 168-mutant battery, about 30 s). Exit 0 = every check of
  `out/summary_e0b2.txt` is PASS (25 lines). Two full runs give the same `out/manifest_e0b2.txt` (checked at T0).
- Needs Python 3 with capstone, the disc EXE named in `oracle/tools/lib.py` and the map JSONs of `data-extracted/data/` (read only). Nothing outside `oracle/out/` is written.
- The scripts are byte copies of the discovery (`s1_table`, `s2_choose`, `s3_roll`, `s5_spawn`, `s6_record`, `s8_veto`, `s10_mutants`, `xdis`, `e14world`, `hworld`) and of the critic (`p1_roll`, `p5_mutants`, `p8_loadmap`,
  `cdis`), except for the corrections listed under "Harness corrections"; `v8.py` and `run_e0b2.py` are written for this slice. `oracle/tools/` now holds the whole tool set of `docs/plan-e20-annexe/e20-e0a/oracle/tools`
  (the 7 files of e0b1 are unchanged); `lib`, `emu`, `machine` and `mips` are also the discovery's. `oracle/checksums.txt` lists the SHA-1 of every file.

## Harness corrections (the two weaknesses of the critic, plan 1.17 note 9)

1. The noise pre-written in `+0x3C` / `+0x40` before each roll was `0xBEEF` / `0xDEAD` (zero high bytes: the mutant `sw $zero, 0x3f($s0)` at `0x80032B00` survived). It is now `0xA5A5A5A5` in `s3_roll.py`,
   `s10_mutants.py`, `p1_roll.py` and `p5_mutants.py`. The fuzz vectors do not change (`roll_fuzz_vectors.json` keeps the md5 `087b48f326be28c6eb90763d933d4054` of the critic); the mutant is now caught.
2. No code `>= 0x100` was tried. `s2_choose.py` now sweeps the codes `0..0x1FF`: `choose_vectors.json` keeps the 2 626 vectors of the codes `0..0xFF`, the new `choose_high_vectors.json` holds the 256 vectors of the codes
   `0x100..0x1FF` (the function returns 0 with no draw, whatever bit `0x80` says); `p5_mutants.py` sweeps `0..0x1FF` too.

## Expected values (the prediction, written before the DLL code)

1. **The roll** (`out/roll_vectors.json` 52, `out/spawn_vectors.json` 183, `out/record_vectors.json` 12, `out/roll_fuzz_vectors.json` 6 000, `out/choose_vectors.json` 2 626, `out/choose_high_vectors.json` 256): with a
   record, the flag `f = hu[rec + 0x10]` is set to 0 when `(f & 0x7FFF) >= 0x800` and written at `+0x40` even when zero; if it is non-zero and its bit is raised (bank `0x801EBA40` when `f & 0x8000`, else `0x801EB344`,
   word `(f >> 5) & 0x3FF`, bit `f & 31`) the code is 0 and nothing is drawn; otherwise the code is `rec[0x12]` when non-zero, else `header[0x1F]`. Without a record `+0x40 = 0` and the code is `header[0x1F]`.
   `ChooseRandomlyAnItem(code)`: `code >= 0x100 -> 0`; `code & 0x80`: one step of the generator, then `code = table[(code & 0x7F) * 16 + (s >> 28)]` and again (at most two draws on this table); else `code < 0x62 ? code : 0`.
   Draws: 0 (code without bit `0x80`, or flag raised), 1 or 2. The 13 non-zero rows are `0x81..0x83` and `0xF6..0xFF`; no table byte is `0xFF`.
2. **V8 by the load tile** (below): the entities the load loop creates, in order, with their items, `+0x40`, draws and the final word, for 20 (map, tile) pairs taken from the ArcSpecs of the suite.
3. **Mutants**: the 13 named mutants of the discovery (12 caught, the 13th is the argued equivalent) and the 168 of the critic (120 caught; the 48 survivors are argued one by one below).
4. **The DLL (prediction, qualitative; Q-B1 "before and after")**: `ArcRun` re-seeds before the load (constructor, `ResetAll`) AND after `InitializeWithWorld` (T1), so the draws of the load no longer move the arcs; what
   remains are the draws of the creations made DURING an arc (`0x2D`, `0x8A`, `0x8B` and the interface `SpawnEntityBySpriteIndex`). At most 3 existing tests move in phase B (the critic's static estimate), all inside
   the predicted list that the shadow run measures in T1. The second re-seed has no effect in phase A: nothing draws during the load today (the word after the load equals the seed). Since E20.f3c the sword
   wave (sprites 27, 28, 29) is born in production through `SpawnEntityBySpriteIndex`: the f3 oracle gives that newborn no loot draw (`loot3c` 0, `docs/plan-e20-annexe/e20-f3/oracle/out/wave_*.json`) because the
   header `Contents` of sprites 27 to 29 is 0; the shadow counts these creations (one virtual roll each, zero virtual steps) and the README names them in the predicted list.

## Starting state (T0 measures, 2026-10-16, base commit `b5f0c6a`, quiet window: nothing else built or ran)

| Measure | Value |
|---|---|
| `Alundra.Tests` Release | 5 046 passed, 0 failed, 5 m 40 s |
| `Alundra.Tests` Debug | 5 046 passed, 0 failed, 6 m 01 s |
| `alundra-project/Alundra.dll` lock | not locked at T0 (every build is nevertheless made with the copy neutralised, the last Debug build excepted) |

## Oracle results (T0)

`run_e0b2.py`: 25 checks, every one PASS (`out/summary_e0b2.txt`); two runs, identical `out/manifest_e0b2.txt`.

| Vectors | Count | Mismatches |
|---|---|---|
| `ChooseRandomlyAnItem` on the machine, every path of the decision tree of the codes `0..0xFF` (`choose_vectors.json`) | 2 626 | 0 |
| the codes `0x100..0x1FF` (`choose_high_vectors.json`) | 256 | 0 |
| the roll `0x80032A40` called on a hand-built entity (`roll_vectors.json`; no write outside `+0x3C`, `+0x40` and the word) | 52 | 0 |
| the roll through the REAL `SpawnWarpEntity` (record-less path), 61 numbers x 3 seeds (`spawn_vectors.json`) | 183 | 0 |
| the roll through the REAL record spawner `0x8003A1B8` (`record_vectors.json`) | 12 | 0 |
| documented vectors of the critic plus 6 000 random ones with flags, two banks, record and header codes (`roll_fuzz_vectors.json`, graine `20261015`) | 6 011 | 0 |

The table of the machine equals the 2 048 bytes of the executable at `0x960C` (md5 `7c6b97ac50de72f436a6fbf08ed6bcd7`).

**Mutants.** `s10_mutants.py`: 13 one-word mutants (choose: draw range, bit-`0x80` polarity, item limit, a branch target; roll: flag threshold, bank bit, record-code byte, header byte, flag-set polarity; frame:
multiplier, `a0` polarity, gate polarity, inventory-result polarity), 12 CAUGHT; the 13th (`choose: branch target one word early`) lands on a delay-slot `slti` whose result is overwritten: equivalent.
`p5_mutants.py` (168 generic one-word mutants of `0x80032968-0x80032A00` and `0x80032A40-0x80032B28`): **120 caught** (the same 120 by the discovery vectors and by the critic's), **48 survivors**, one by one:

| Survivors | Count | Why they are equivalent |
|---|---|---|
| `nop` shifted (`sll $zero,$zero,x`, `ssnop`) at `0x800329FC`, `0x80032A54`, `0x80032A5C`, `0x80032A64`, `0x80032A74`, `0x80032A94`, `0x80032AE4`, `0x80032AEC`, `0x80032AF8`, `0x80032B24` | 20 | the destination is `$zero`: no effect |
| stack frame: `addiu $sp` at `0x80032A40` and `0x80032B1C` (3 each), `sw $s0` at `0x80032A44` and `lw $s0` at `0x80032B18` (3 each) | 12 | only the saved `$s0` / `$sp` of the caller move; the harness reads the entity and the word, never the callee-saved registers after the return |
| `sra $v1,$a3,0x1f` shift `+-1` at `0x800329D4` | 2 | `$v1` (the high-word sign of a product) is never read |
| `slti $v0,$a1,0x100` at `0x8003296C` (imm `0x101`) | 2 | only the code `0x100` differs, and `0x100` has bit `0x80` clear and is `>= 0x62`: it returns 0 with no draw either way |
| `slti $v0,$a1,0x100` at `0x800329E4` (imm `0x101`, `0xff`) | 3 | the tested value is a table byte (`lbu`); no table byte is `0xFF` (checked in `loot_table.json`), so `< 0x100`, `< 0x101` and `< 0xff` all hold |
| `andi $v0,$v1,0x7fff` -> `0x7ffe` at `0x80032A68` | 2 | bit 0 never decides `>= 0x800` |
| the rounding to 32 of the bit number: `bgez`/`bltz` at `0x80032AB0`, `addiu` at `0x80032AB8` (3), `sra` at `0x80032ABC` (2), `sll` at `0x80032AC0` (1, `sa+1`) | 7 | `sllv` keeps only the 5 low bits of the shift count and the rounded value is a multiple of 32, so `flag - rounded` is `flag mod 32` in every variant |

Total 20 + 12 + 2 + 2 + 3 + 2 + 7 = 48, the printed list of `out/p5_mutants.stdout.txt`.
The mutant `sw $zero, 0x3f($s0)` (`0x80032B00`), a survivor of the critic with the old noise, is now CAUGHT.

## V8: the real load loop by the load tile of the hero

The tile that the zone gate of the binary (`0x8003A1F4-0x8003A25C`) reads is the hero's `+0x120 / +0x124`. In the DLL the hero is placed by `AdoptPlayerPawn` BEFORE the loop (it reads
`PlayerEntity.TileX/Y`): at the centre of the New Game tile `(AlundraGameState.CameraTileX, CameraTileY) = (33, 59)` for an arc WITHOUT arrival, at `((PosX >> 16) / 24, (PosY >> 16) / 16)` of the arrival record for an
arc WITH an arrival. `ArcRun` writes the tile of its `ArcSpec` (`HeroTileX/Y`) only after `InitializeWithWorld`, so it is irrelevant to the load. `out/v8.json` gives, per pair, the load tile, the records called, the
entities created in order (record index, slot, sprite number, item, `+0x40`, draws), the draws and the final word, from the seed `0xB017C93D` (= `AlundraRandom.Reset()`, which `ArcRun` applies before the load).
The table lists, per pair, the creations that roll or carry a flag (`r<record>:s<sprite> -> <item>/f<+0x40>/<draws>d`).

| Map | Load tile | How the DLL test reaches it (arcs of the suite) | Records called | Entities | Draws | Final word | Creations that roll or carry a flag |
|---|---|---|---|---|---|---|---|
| 14 | (33, 59) | arc without arrival: AdoptPlayerPawn places the hero on the New Game tile | 25 | 19 | 11 | `D5F4D64A` | r0:s335->0/1d, r1:s335->71/2d, r2:s335->0/1d, r3:s335->0/1d, r4:s335->0/1d, r5:s335->0/1d, r6:s335->71/2d, r13:s30->36/f1720, r16:s254->84/1d, r17:s238->0/1d, r19:s30->83/f1665, r20:s30->79/f1351 |
| 390 | (33, 59) | arc without arrival | 16 | 16 | 12 | `BBE29AC9` | r4:s292->85/1d, r5:s292->0/1d, r6:s292->0/1d, r7:s292->0/1d, r8:s292->84/1d, r9:s292->84/1d, r10:s292->84/1d, r11:s292->0/1d, r12:s292->0/1d, r13:s292->84/1d, r14:s292->0/1d, r15:s292->0/1d |
| 392 | (33, 59) | arc without arrival | 12 | 12 | 12 | `BBE29AC9` | r0:s292->85/1d, r1:s292->0/1d, r2:s292->0/1d, r3:s292->0/1d, r4:s292->84/1d, r5:s292->84/1d, r6:s292->84/1d, r7:s292->0/1d, r8:s292->0/1d, r9:s292->84/1d, r10:s292->0/1d, r11:s292->0/1d |
| 389 | (33, 59) | arc without arrival | 19 | 14 | 0 | `B017C93D` | - |
| 391 | (33, 59) | arc without arrival | 7 | 6 | 0 | `B017C93D` | - |
| 478 | (33, 59) | arc without arrival | 22 | 22 | 0 | `B017C93D` | - |
| 115 | (3, 7) | arc with arrival (84, 120) px, z 1 (LeverArcs.Spec) | 34 | 4 | 3 | `E4013D62` | r29:s292->85/1d, r30:s292->0/1d, r31:s292->0/1d |
| 116 | (3, 20) | arc with arrival (84, 328) px, z 0 (LeverArcs.Spec) | 49 | 27 | 15 | `9169E31E` | r0:s335->0/1d, r1:s335->71/2d, r9:s292->0/1d, r16:s30->36/f657, r17:s335->0/1d, r20:s290->84/1d, r21:s290->84/1d, r41:s335->71/2d, r42:s335->0/1d, r43:s335->70/2d, r44:s335->0/1d, r45:s292->0/1d, r46:s292->0/1d |
| 117 | (20, 12) | arc with arrival (500, 192) px, z 1 (LeverArcs.Spec) | 32 | 3 | 3 | `E4013D62` | r15:s335->0/1d, r16:s335->71/2d |
| 117 | (3, 40) | arc with arrival (72, 640) px, z 2 (LeverArcs.Spec) | 32 | 5 | 4 | `88CB7281` | r5:s335->0/1d, r7:s335->71/2d, r9:s335->0/1d |
| 44 | (5, 27) | arc with arrival (120, 440) px, z 8 (LeverArcs.Spec) | 38 | 21 | 6 | `21B6529B` | r13:s30->36/f1631, r14:s30->79/f1632, r17:s335->0/1d, r18:s335->71/2d, r19:s335->0/1d, r20:s335->0/1d, r21:s335->0/1d |
| 162 | (30, 26) | arc with arrival 47972352, 27787264 | 16 | 16 | 0 | `B017C93D` | - |
| 165 | (12, 22) | arc with arrival 19660800, 23592960 | 28 | 4 | 1 | `35E36190` | r10:s291->85/1d |
| 185 | (38, 23) | arc with arrival 60555264, 24641536 | 11 | 9 | 0 | `B017C93D` | - |
| 179 | (17, 7) | arc with arrival at tile (17, 7), z 1 (day3-after-dream and many) | 27 | 8 | 1 | `35E36190` | r5:s291->85/1d |
| 176 | (7, 34) | arc with arrival 11796480, 36175872, z 10 | 17 | 13 | 0 | `B017C93D` | - |
| 10 | (10, 58) | arc with arrival 16515072, 61341696 | 117 | 14 | 2 | `C81B4C37` | r99:s254->85/1d, r116:s254->0/1d |
| 135 | (19, 51) | arc with arrival 30670848, 54001664, z 1 | 29 | 6 | 0 | `B017C93D` | - |
| 178 | (38, 25) | arc with arrival 60555264, 26738688 | 24 | 7 | 3 | `E4013D62` | r17:s291->85/1d, r18:s291->0/1d, r19:s291->0/1d |
| 167 | (33, 59) | NO ArcSpec of the suite names map 167 (its only test, RealMap167_OneFullRunnerCallFrom0x5AAt144, loads the program without a world); an ad-hoc ArcSpec without arrival is the DLL way | 27 | 12 | 8 | `C5BF9705` | r19:s292->85/1d, r20:s292->0/1d, r21:s292->0/1d, r22:s292->0/1d, r23:s292->84/1d, r24:s292->84/1d, r25:s292->84/1d, r26:s292->0/1d |

Arcs of the suite per pair (`v8.json`, field `arcs`): see the file. **Map 167** has no ArcSpec in the suite (its only test, `RealMap167_OneFullRunnerCallFrom0x5AAt144_DrawsOnceAndReachesTheWaitAt152`, loads the
program without a world): the pair is measured at the New Game tile for an ad-hoc `ArcSpec` without arrival (the m2 test of T1 builds it); the plan names map 167 for the arc of the `0x5A` mode 4, whose real
source is this test. The flag banks are empty in every pair (an `ArcSpec` with `Flags` raises bits and would change the pair: m2 uses `Flags = []`).

**The critic's pairs, re-run only to prove the harness** (`v8.json`, field `reproduction`, no acceptance value for the DLL): 14 at (8, 6) -> `D5F4D64A`, 11 draws, 19 entities; 390 at (0, 0) -> `BBE29AC9`, 12 draws, 16
entities; 392 at (0, 0) -> `BBE29AC9`, 12 draws, 12 entities. All three reproduce. Note that map 14 at the load tile (33, 59) gives the same entities and the same word as at (8, 6): the window of the
first records covers both.
