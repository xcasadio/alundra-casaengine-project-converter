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

---

## Phase A (T1): the code, the shadow run, the predicted list

Code commit `4753357` (phase A: `AlundraLootRoll`, `HeaderContents` and its one line of the core, the helper `RollLootAtCreation` at the three creation sites, the virtual roll of the sink, the second re-seed of `ArcRun`,
`SeedAfterTheLoad` and `HeroTileAtLoad`). Starting count 5 046 (Release and Debug); after phase A **5 057 in Release and 5 057 in Debug, zero failure**: the 11 new tests (`AlundraLootRollTests` 8,
`AlundraLootRollWorldTests` 3: M0, the word after a quiet load, the inertness of the helper) and nothing else moved. The second re-seed of `ArcRun` is without effect in phase A, as the plan says (no existing test moved).
`git diff` of `AlundraEntitySpawnFactory.cs`: the single line `proxy.HeaderContents = header.Contents;`.

### The shadow run (`e0b2-shadow.txt`)

`ALUNDRA_RNG_SHADOW_LOG=<file> dotnet test ... -c Release --no-build` on the code commit: 5 057 passed (the inertness tests are no-ops with the sink on), 6 min 18 s; **1135 lines, 499 distinct attributed keys** (constructors and `Dispose`
included). Totals: 158453 virtual advances (the e0b1 callback, which also writes the real state since its phase B: it never makes V and R diverge), 26960 real draws, **11938 virtual rolls (creations) in 283 keys, 4856 virtual steps**,
28123 epochs, 114 divergent draws, 481 divergent seed reads, 233 ambient draws. The predicate is at the head of the file.

### Predicted list (rule of e0b1: `divergent_draws + divergent_seed_reads + ambient_draws > 0`)

**248 tests**, in three groups (columns: virtual advances, draws, divergent draws, divergent seed reads, epochs, ambient draws, virtual rolls = creations, virtual steps, first divergent draw):

1. **One test with divergent draws** (its AI draws after creations that the second re-seed of `ArcRun` does not cover, the ones made DURING the arc): `AlundraMurutaHallArcTests.H115_...` (114 divergent draws, first in `AlundraEnemyHelpers.Below`; 6 creations seen, 7 virtual steps,
   the load's and the arc's together).
   This is the only existing test where phase B changes a drawn number; it is the one the plan's estimate ("at most 3") expects to move.
2. **16 tests with ambient draws** (state inherited from the order of the tests), the same families as e0b1: handled below.
3. **231 tests with only divergent seed reads**: every one is an arc on a map whose load rolls (maps 14, 390, 392, 115, 116, 117, 44, 165, 179, 10, 178, 167...), where `ArcRun` itself reads `RandomSeed` (for `SeedAfterTheLoad`) right before
   its second `Reset()`: the read is divergent in phase A by construction (the virtual rolls of the load moved V, not R), and the value is thrown away by the `Reset()` that follows. A test cannot move because of it unless it reads
   `arc.SeedAfterTheLoad` (only the new tests of this slice do). They are kept in the list because the rule says so; they are listed after the first two groups.

The sword wave born in production since E20.f3c (sprites 27, 28, 29, `SpawnEntityBySpriteIndex`, header `Contents` 0, the f3 oracle's `loot3c` 0) is counted: the creation goes through the helper (one virtual roll) and makes **0 virtual
steps**. Seen in the keys of `AlundraWaveSwordWiringTests` (P1 to P7b), `AlundraWaveSwordLifeTests` (V1, V2, V4), `AlundraWeaponSpawnSeamTests` (S1 to S7) and `AlundraHeroChargeArenaTests`, whose creations beyond the 19 entities of the load of
map 14 add no step: e.g. P3 and P4, one arc: 24 creations and 11 virtual steps, the 19 entities of the load (11 steps, pair "14 at (33, 59)" of V8) plus 5 creations that step nothing.

| Test | virtual advances | draws | divergent draws | divergent seed reads | epochs | ambient draws | virtual rolls | virtual steps | first divergent draw |
|---|---|---|---|---|---|---|---|---|---|
| `AlundraHeroAuraTests.AHeldCharge_RenewsTheAuraEvery97Ticks_AtTheTicks33_130_227_InTheSlotsOfTheOracle_OneAliveAfterEachUpdate` | 0 | 108 | 0 | 0 | 1 | 108 | 0 | 0 | - |
| `AlundraHeroRunTests.G7_TheSacredSword_CountsTheChargeInEveryStateOfTheRun` | 0 | 42 | 0 | 0 | 0 | 42 | 0 | 0 | - |
| `AlundraHeroSwimTests.W6_TheOtherWeaponTraces_AreTheOracle` | 0 | 4 | 0 | 0 | 0 | 4 | 0 | 0 | - |
| `AlundraHeroSwimTests.W6_TheSquareHeld_ChargesOnTheLand_ZeroInTheWater_AndOnTheLandAgain` | 0 | 36 | 0 | 0 | 0 | 36 | 0 | 0 | - |
| `AlundraMurutaHallArcTests.H115_TheFourMuruta_DieByTheSword_TheDoorOfTheHallOpens_WithAFrameBreakEffectEach` | 374 | 114 | 114 | 1 | 3 | 0 | 6 | 7 | ai:AlundraEnemyHelpers.Below |
| `AlundraNativeEffectTests.TheVessel_OnTheNewGameStats_Answers1_DoesNotSuspend_RaisesTheLife_PlaysTheSoundBeforeTheEffects_AndCreatesTwelveSlots` | 0 | 8 | 0 | 0 | 0 | 8 | 0 | 0 | - |
| `AlundraNativeEffectTests.TheVessel_ReadsTheHeroWhereItIsWhenTheScriptRuns_NotWhenTheStageApplies` | 0 | 8 | 0 | 0 | 0 | 8 | 0 | 0 | - |
| `AlundraNativeEffectTests.WithoutASoundPlayer_TheLifeAndTheEffectsAreDone` | 0 | 8 | 0 | 0 | 0 | 8 | 0 | 0 | - |
| `AlundraPrefabArcSupportTests.ArcRun_ResetsTheSharedRandomStream_AtConstructionAndAtDispose` | 0 | 4 | 0 | 1 | 3 | 3 | 16 | 12 | - |
| `AlundraRandomBranchOpcodeTests.IfRandomAtLeast_0x8C_TheComparisonIsGreaterOrEqual` | 0 | 2 | 0 | 0 | 1 | 1 | 0 | 0 | - |
| `AlundraRandomBranchOpcodeTests.IfRandomAtLeast_0x8C_TheFirstDrawIs53_SoThreshold128GivesZero_AndTheSeedAdvancesOnce` | 0 | 1 | 0 | 0 | 0 | 1 | 0 | 0 | - |
| `AlundraRandomBranchOpcodeTests.IfRandomAtLeast_0x8C_TwoCallsDrawTwiceFromTheSameStream` | 0 | 2 | 0 | 0 | 0 | 2 | 0 | 0 | - |
| `AlundraRandomOpcodeTests.SetRandomCardinalDirection_0x0C_FiveDrawsGiveTheBinarysDirections_AndTheSeedAdvancesOnce` | 0 | 5 | 0 | 0 | 0 | 5 | 0 | 0 | - |
| `AlundraRandomTests.Next_ReproducesTheOriginalsFirstThreeValues_HandComputedFromTheLcg` | 0 | 3 | 0 | 0 | 0 | 3 | 0 | 0 | - |
| `AlundraRandomTests.Next_ReturnsOnlyTheLow32BitsOfTheWrappedSeed` | 0 | 1 | 0 | 0 | 0 | 1 | 0 | 0 | - |
| `AlundraRandomTests.Reset_SetsTheSeedBackToTheOriginalConstant` | 0 | 1 | 0 | 0 | 1 | 1 | 0 | 0 | - |
| `AlundraStoryChainSkippedOpcodesTests.Oracle_AnOpcodeTheRunnerHandlesIsPorted_OneItSkipsOrCannotSizeIsNot` | 0 | 2 | 0 | 0 | 0 | 2 | 0 | 0 | - |
| `AlundraAbsoluteZWritesTests.Map163_TheCrateOfRecord15_StaysOnTheCrateOfRecord14` | 60 | 0 | 0 | 1 | 3 | 0 | 18 | 8 | - |
| `AlundraAbsoluteZWritesTests.Map179_TheCrateOfRecord12_StaysOnTheCrateOfRecord11` | 60 | 0 | 0 | 1 | 3 | 0 | 10 | 2 | - |
| `AlundraAbsoluteZWritesTests.Map390_TheCrateOfRecord6_StaysOnTheCrateOfRecord4` | 60 | 0 | 0 | 1 | 3 | 0 | 16 | 12 | - |
| `AlundraAbsoluteZWritesTests.Map83_TheFloatingPlatform_EndsItsFirstClimbAtThe33rdCall_At48Pixels` | 34 | 0 | 0 | 1 | 3 | 0 | 15 | 15 | - |
| `AlundraAnimationImpulseSpawnTests.UJ7_TheHeroArrivingByThe0x53LaunchedDuringTheAnimation2_TakesNoImpulse_AndStaysAtItsHeightFor5Ticks` | 7 | 0 | 0 | 1 | 3 | 0 | 10 | 1 | - |
| `AlundraAnimationSoundTests.T1_Map178_TheBookOfTheTickProgram728_AsksThePageSound204Once` | 1981 | 0 | 0 | 1 | 3 | 0 | 8 | 3 | - |
| `AlundraAnimationSoundTests.T1_Map179_TheBookSpawnedLikeBy0x8A_At206_AsksThePageSound204Once_When1A01Switches` | 123 | 0 | 0 | 1 | 3 | 0 | 11 | 1 | - |
| `AlundraAnimationSoundTests.T1_Map392_0x5B_At64_AsksTheHeroSprintSound13_Once` | 205 | 0 | 0 | 1 | 3 | 0 | 12 | 12 | - |
| `AlundraAnimationSoundTests.T1_TheHeroArrivingByAdoptPlayerPawnInTheAnimation43_AsksSound10_AndTheFirstSwitchAsksNoSecond` | 2 | 0 | 0 | 1 | 3 | 0 | 10 | 1 | - |
| `AlundraArcGuardAndSoundHostTests.TReg0_AGreenArcOfTheGuardedSet_ThatMetAnEntity_FailsAtItsDispose` | 1 | 0 | 0 | 1 | 3 | 0 | 10 | 1 | - |
| `AlundraArcGuardAndSoundHostTests.TReg0_AnArcThatNeverMetAnEntity_DisposesWithoutFailure_AndSoDoesAnArcOutsideTheGuardedSet` | 2 | 0 | 0 | 2 | 6 | 0 | 20 | 2 | - |
| `AlundraArcGuardAndSoundHostTests.TReg0_WhenTheTestIsAlreadyFailing_TheGuardDoesNotReplaceTheOriginalFailure` | 1 | 0 | 0 | 1 | 3 | 0 | 10 | 1 | - |
| `AlundraArcInventoryHelpersTests.Equip_ASlotWithNoOwnedItem_ResolvesToNoItem_AndMinusOneUnequips` | 0 | 0 | 0 | 1 | 3 | 0 | 14 | 2 | - |
| `AlundraArcInventoryHelpersTests.GiveThenEquip_TheHuntersBow_MakesItTheCurrentWeapon_AndTheHeroDrawsIt` | 2 | 0 | 0 | 1 | 3 | 0 | 19 | 2 | - |
| `AlundraArcInventoryHelpersTests.Give_SeveralItems_GivesOneOfEach_AndAGivenWeaponStaysAtItsMaximumOfOne` | 0 | 0 | 0 | 1 | 3 | 0 | 14 | 2 | - |
| `AlundraArcInventoryHelpersTests.SetItem_PosesTheCircleItem_AndRefusesAnIdPastTheTable` | 0 | 0 | 0 | 1 | 3 | 0 | 14 | 2 | - |
| `AlundraArcInventoryHelpersTests.SetMp_AppliesTheCeilingOf4_AndClampsTheMagicToItsMaximum` | 0 | 0 | 0 | 1 | 3 | 0 | 14 | 2 | - |
| `AlundraBeannoideGrateArcTests.G116_TheTwoBeannoids_DieByTheSword_AndTheGrateOpens_WithAFrameBreakEffectEach` | 312 | 90 | 0 | 1 | 3 | 0 | 27 | 15 | - |
| `AlundraBergusJumpArcTests.A12_BergusJumpsOnMap179_TheWaitForTheLandingEnds_TheSceneLeavesByThe0x53` | 2276 | 0 | 0 | 1 | 3 | 0 | 10 | 1 | - |
| `AlundraBirthBoxTests.B1_R_TheBoxOfTheWave_IsTheOneOfTheBinaryAtEveryTick_InTheFourDirectionsOfTheSteelSword` | 363 | 0 | 0 | 13 | 39 | 0 | 312 | 143 | - |
| `AlundraBirthBoxTests.B2_R_HasFrameBoxAtTheCreation_IsTheHasBoxOfTheOracle_OnTheVectorsOfTheInterfaceTest_AndTheVectorsAtZeroStayFalse` | 0 | 0 | 0 | 1 | 3 | 0 | 175 | 20 | - |
| `AlundraBirthBoxTests.B3_R_BeforeTheIntegrationTheSpriteHasNoAnimationAndTheEntityHasItsBox_AfterItTheBoxOfSPlus1IsTheImage1_AndWithoutResourcesTheTimelineIsNotFrozenAtNull` | 5 | 0 | 0 | 2 | 6 | 0 | 48 | 22 | - |
| `AlundraBirthBoxTests.B4_R_ASwitchBeforeTheIntegration_GivesTheBoxOfTheAnimation1AtS_AndAtTheTicksThatFollow` | 10 | 0 | 0 | 1 | 3 | 0 | 24 | 11 | - |
| `AlundraBirthBoxTests.B5_R_AfterTheValidation_TheCursorOfTheBox_IsTheLogicalTickOfTheSprite_AtTheEndOfEveryFrame` | 166 | 0 | 0 | 5 | 15 | 0 | 120 | 55 | - |
| `AlundraBirthBoxTests.B6_V_AnEntityBornFromARecord_HasNoBoxAtItsCreation_AndTheHostIsNeverAsked_AndAHostWithoutResourcesIsAskedAtMostOnce` | 0 | 0 | 0 | 1 | 3 | 0 | 21 | 12 | - |
| `AlundraBirthTickProbePaTests.PA1_TheSpriteAskedForItsFirstAnimationBeforeTheIntegration_ThenIntegrated` | 1 | 0 | 0 | 1 | 3 | 0 | 24 | 11 | - |
| `AlundraBirthTickProbePaTests.PA1b_WithCurrentEqualTarget_PosedByHand_TheFirstFrameAfterTheIntegration` | 1 | 0 | 0 | 1 | 3 | 0 | 24 | 11 | - |
| `AlundraBirthTickProbePaTests.PA2_ControllerMoveBeforeTheIntegration_AddsTheDisplacementWithoutATest_AndAfterIt` | 1 | 0 | 0 | 1 | 3 | 0 | 24 | 11 | - |
| `AlundraBirthTickProbePaTests.PA3_TheGenericKinematicsOfTheBirthTick_WithCurrentAsTheInterfaceLeavesIt_MeasuredAfterE0a3b` | 0 | 0 | 0 | 1 | 3 | 0 | 20 | 11 | - |
| `AlundraBirthTickProbePaTests.PA3_TheGenericKinematicsOfTheBirthTick_WithCurrentEqualTarget_PosedByHand` | 0 | 0 | 0 | 1 | 3 | 0 | 20 | 11 | - |
| `AlundraBirthTickTests.AnEntityFromARecord_KeysItsKinematicsOnItsCurrentAnimation_NeverOnTheTarget_EvenWithCurrentStillNotValidated` | 0 | 0 | 0 | 1 | 3 | 0 | 20 | 11 | - |
| `AlundraBirthTickTests.BT_H_BornInTheNativeProgramOfAnotherEntity_InATwoTickFrame_RunsAtTheSecondTick_NotInTheFirst` | 2 | 0 | 0 | 1 | 3 | 0 | 25 | 11 | - |
| `AlundraBirthTickTests.BT_H_BornInTheNativeProgramOfAnotherEntity_RunsFromTheNextTick_TheGapOfD_E20_34_OneTickFrames` | 2 | 0 | 0 | 1 | 3 | 0 | 25 | 11 | - |
| `AlundraBirthTickTests.BT_M_BornInMovePlayer_TakesPartInTheTickOfItsBirth_ProgramListsClockPhysics` | 3 | 0 | 0 | 1 | 3 | 0 | 24 | 11 | - |
| `AlundraBirthTickTests.BT_M_BornInTheFirstTickOfATwoTickFrame_ClockAndPhysicsRunTwice_AndANativeBirthIsNotRunTwice` | 3 | 0 | 0 | 1 | 3 | 0 | 24 | 11 | - |
| `AlundraBirthTickTests.DestroyedInTheTickOfItsBirth_InATwoTickFrame_IsRecycledAtTheHeadOfTheNextTick_ItsSlotIsGivenBack_AndTheEngineIntegratesNothingWrong` | 5 | 0 | 0 | 1 | 3 | 0 | 25 | 11 | - |
| `AlundraBirthTickTests.T0Prime_AnimFlagsAtBirth_ThenTheFirstFrameValidatesThePendingSwitch_InTheFourDirections` | 8 | 0 | 0 | 8 | 24 | 0 | 192 | 88 | - |
| `AlundraBirthTickTests.TheCreation_NeverReadsTheCollisionFieldOfTheHostByTheFallback_TheBirthTickDoes_AndAnEntityFromARecordNever` | 4 | 0 | 0 | 1 | 3 | 0 | 26 | 12 | - |
| `AlundraBirthTickTests.TheMarker_IsPosedByTheInterfaceOnly_AndNotCloned` | 0 | 0 | 0 | 1 | 3 | 0 | 20 | 11 | - |
| `AlundraBirthTickTests.TheSoundOfTheCreation_IsRequestedOnce_WhenTheEntityTakesPartInTheTickOfItsBirth` | 5 | 0 | 0 | 1 | 3 | 0 | 24 | 11 | - |
| `AlundraControllerlessVerticalStepTests.Gate_ARealPrefabWithoutController_HasTheRawMapWordsOfItsMap` | 0 | 0 | 0 | 1 | 6 | 0 | 15 | 8 | - |
| `AlundraControllerlessVerticalStepTests.Real_Map260_TheArmourOfRecord17_Rises32Pixels_InExactly64Calls` | 69 | 0 | 0 | 1 | 3 | 0 | 13 | 8 | - |
| `AlundraCreationSwitchClockTests.A1_R_TheWave_RaisesItsEndFlagAtSPlus25_WhateverTheGroupingAndThePositionInTheFrame_AndOneTickLaterWithoutTheFlag` | 1004 | 0 | 0 | 27 | 81 | 0 | 648 | 297 | - |
| `AlundraCreationSwitchClockTests.A2_R_BornInTheProgramsOfAnotherEntity_TheFirstClockTickIsSPlus1_AndItsCursorIs1` | 32 | 0 | 0 | 4 | 12 | 0 | 96 | 44 | - |
| `AlundraCreationSwitchClockTests.A2_R_TheCursorOfTheBox_IsTheCnt1cOfTheBinary_FromTheTickOfTheBirth_InTheFourDirections` | 63 | 0 | 0 | 8 | 24 | 0 | 192 | 88 | - |
| `AlundraCreationSwitchClockTests.A3_R_TheLoopCounter_TurnsAtTheTicksOfTheBinary` | 100 | 27 | 0 | 6 | 18 | 0 | 144 | 66 | - |
| `AlundraCreationSwitchClockTests.A4_R_AFrameWithoutATickBetweenTheBirthAndTheValidation_DoesNotDelayTheFlag_AndAValidationWithZeroTicksReservedOwesNoTick` | 40 | 0 | 0 | 2 | 6 | 0 | 48 | 22 | - |
| `AlundraCreationSwitchClockTests.A5_OneTickPerFrame_ASwitchInTheWindow_EndsWhereTheBinaryEndsIt` | 200 | 0 | 0 | 5 | 15 | 0 | 120 | 55 | - |
| `AlundraCreationSwitchClockTests.A5_R_ThreeTicksPerFrame_BornAtTheFirstTick_ASwitchInTheWindow_EndsWhereTheBinaryEndsIt` | 40 | 0 | 0 | 1 | 3 | 0 | 24 | 11 | - |
| `AlundraCreationSwitchClockTests.A5_R_TwoTicksPerFrame_BornAtTheFirstTick_ASwitchInTheWindow_EndsWhereTheBinaryEndsIt` | 82 | 0 | 0 | 2 | 6 | 0 | 48 | 22 | - |
| `AlundraCreationSwitchClockTests.A6_V_AnEntityBornFromARecord_KeepsItsFirstClockTickUncounted_KnownDeviationQE0A5_1` | 5 | 0 | 0 | 1 | 3 | 0 | 24 | 13 | - |
| `AlundraCreationSwitchClockTests.A6_V_TheCreationSoundIsRequestedOnce_AndNoImpulseIsGivenInTheWindowOfTheWave` | 9 | 0 | 0 | 1 | 3 | 0 | 24 | 11 | - |
| `AlundraDay3SceneArcTests.A14_TheVillagersOnMap179_NestusClearsT800AndT801_TheHeroStopsAtSeptimus_AndLeavesFor176` | 754 | 0 | 0 | 1 | 3 | 0 | 10 | 1 | - |
| `AlundraDay3SceneArcTests.A18_TheHouseOnMap178_TwoPhases_TheTablesSay183_G203IsClearedAndG204Set` | 3193 | 0 | 0 | 1 | 3 | 0 | 10 | 3 | - |
| `AlundraEnemyGroundTests.ProductionSpawn_OfTheMap14Beannoids_HoldsTheBinarysCreationValuesMinusOne` | 10 | 0 | 0 | 1 | 3 | 0 | 23 | 11 | - |
| `AlundraEnemyHitReactionTests.HeroAnimationLatch_OfTheContext_DefaultsToZero_AndTheWorldReturnsItsHitPassLatch` | 16 | 0 | 0 | 1 | 3 | 0 | 23 | 11 | - |
| `AlundraEntityContactArcTests.TA10v_TheVillagersOnMap10_NestusStopsAtMeade_TheSceneReachesItsEnd` | 5100 | 0 | 0 | 1 | 3 | 0 | 36 | 2 | - |
| `AlundraEntityContactArcTests.TB9_CounterProof_AHeroPlacedInTheSouthHalfOfTheTileParksInTheWayOfSeptimus_TheSceneStalls` | 1400 | 0 | 0 | 1 | 3 | 0 | 28 | 2 | - |
| `AlundraEntityContactArcTests.TB9_SeptimusAndTheBlockOnMap10_BothStopAtRec41_TheSceneSetsG482` | 3632 | 0 | 0 | 1 | 3 | 0 | 29 | 2 | - |
| `AlundraEntityContactArcTests.TC61_TheCartRailsOnMap61_TheHeroStopsAtEachBlock_TheSceneSetsG672` | 337 | 0 | 0 | 1 | 3 | 0 | 16 | 6 | - |
| `AlundraEntityContactArcTests.TH4_TheArrivalOfTheChainOverlapsNoEntity` | 11 | 0 | 0 | 6 | 33 | 0 | 129 | 10 | - |
| `AlundraEntityContactArcTests.TN3_ThePlatformOfMap346_StopsAtTheIronBallWall_AndStaysThere_WithoutDetour` | 659 | 0 | 0 | 1 | 3 | 0 | 20 | 16 | - |
| `AlundraEntitySlotV5RowsTests.V5_dirty_AFreeSlotWithLeftovers_IsTakenLowestFirst_AndTheCreationOverwritesWhatItWrites` | 0 | 0 | 0 | 1 | 3 | 0 | 21 | 11 | - |
| `AlundraEntitySlotV5RowsTests.V5_invalid_full_AnInvalidIndexWithAFullTable_ReturnsNothing_WithTheMessageOfThePage_AndTakesNothing` | 0 | 0 | 0 | 1 | 3 | 0 | 63 | 11 | - |
| `AlundraEntitySlotV5RowsTests.V5_probes_TheThirteenInvalidIndicesOfTheOracle_ReturnNothing_WithOneMessage_AndTakeNothing` | 0 | 0 | 0 | 1 | 3 | 0 | 19 | 11 | - |
| `AlundraEntitySlotWorldTests.ADestroyedEntityKeepsItsSlot_UntilTheRecyclingPassOpens_ThenTheSlotIsFree` | 0 | 0 | 0 | 1 | 3 | 0 | 22 | 11 | - |
| `AlundraEntitySlotWorldTests.AMapLoadWithAFullTable_SkipsTheRecordsThatFindNoSlot_AndLogsEach` | 0 | 0 | 0 | 2 | 6 | 0 | 21 | 14 | - |
| `AlundraEntitySlotWorldTests.AnApparitionTakesTheLowestFreeSlot_AndTheListFollowsTheOrderOfTheSlots` | 0 | 0 | 0 | 1 | 3 | 0 | 23 | 11 | - |
| `AlundraEntitySlotWorldTests.AnExceptionAfterTheAddition_AtTheMapLoad_SkipsThatRecordOnly_AndGivesItsSlotBack` | 0 | 0 | 0 | 2 | 6 | 0 | 37 | 21 | - |
| `AlundraEntitySlotWorldTests.AnExceptionAfterTheAddition_LeavesNothingBehind_NullIsReturned_AndTheSlotIsFree` | 0 | 0 | 0 | 2 | 6 | 0 | 40 | 22 | - |
| `AlundraEntitySlotWorldTests.AnInvalidIndexWithAFullTable_IsRefusedForTheIndex_NotForTheTable_AsTheBinaryChecksThePageFirst` | 0 | 0 | 0 | 1 | 3 | 0 | 63 | 11 | - |
| `AlundraEntitySlotWorldTests.ChildEntity_OnTheRecordPath_IsNullForAMapLoadEntity_ThereIsNoParent` | 0 | 0 | 0 | 1 | 3 | 0 | 19 | 11 | - |
| `AlundraEntitySlotWorldTests.ChildEntity_OnTheRecordPath_IsTheRootOfTheChainOfParents_AsTheBinaryWritesIt` | 0 | 0 | 0 | 1 | 3 | 0 | 22 | 15 | - |
| `AlundraEntitySlotWorldTests.ChildEntity_ThroughBothPaths_RecordThenInterfaceThenRecord_KeepsTheSameRoot` | 0 | 0 | 0 | 1 | 3 | 0 | 22 | 14 | - |
| `AlundraEntitySlotWorldTests.TheEntriesWithoutASlot_StayWhereTheyAre_AndTheInsertionSkipsThem` | 0 | 0 | 0 | 1 | 3 | 0 | 24 | 11 | - |
| `AlundraEntitySlotWorldTests.TheHeroHoldsSlot0_TheLoadedEntitiesTakeTheSlotsFrom1InTheOrderOfTheRecords_AndTheTableHoldsThemAll` | 0 | 0 | 0 | 1 | 3 | 0 | 19 | 11 | - |
| `AlundraEntitySlotWorldTests.TheSixtyFourthApparition_ByRecordId_IsRefused_WithALogLine_AndNothingAdded` | 0 | 0 | 0 | 1 | 3 | 0 | 63 | 12 | - |
| `AlundraEntitySlotWorldTests.TheSixtyFourthApparition_ThroughTheInterface_IsRefused_WithOneLogLine_AndNothingAdded` | 0 | 0 | 0 | 1 | 3 | 0 | 63 | 11 | - |
| `AlundraFlailBounceArcTests.AFlailAtTheHammerPost_BouncesFor16Ticks_AndTheRelationsOfTheBinaryHold` | 244 | 36 | 0 | 4 | 12 | 0 | 92 | 100 | - |
| `AlundraFlailBounceArcTests.AFlailThatTouchesABeannoid40PixelsAway_BouncesAtTheNextTick_For16Ticks_ThenIdle_WithNoForce` | 100 | 18 | 0 | 2 | 6 | 0 | 46 | 22 | - |
| `AlundraFlailBounceArcTests.AFlailWithNothingInReach_160PixelsAway_NeverTouchesNorBounces_TheSwingEndsOnItsOwn` | 100 | 0 | 0 | 2 | 6 | 0 | 46 | 22 | - |
| `AlundraFlailBounceArcTests.Map69_Loads_TheHammerPostOfRecord0Stands_AndTheHeroSettlesFreeOnTheGround` | 1 | 0 | 0 | 1 | 3 | 0 | 23 | 25 | - |
| `AlundraFlailBounceWallProbeTests.FIronNoHit_OnAFlatField_NothingBounces_TheSwingEndsAtTick32` | 50 | 0 | 0 | 1 | 3 | 0 | 23 | 11 | - |
| `AlundraFlailBounceWallProbeTests.FIronWall1_AWallOfHeight1_BouncesAtTick10` | 50 | 0 | 0 | 1 | 3 | 0 | 23 | 11 | - |
| `AlundraFlailBounceWallProbeTests.FIronWall2_AWallOfHeight2UnderTheBoxCorner_BouncesTheIronFlailAtTick10_ReturnsToIdleAtTick26` | 50 | 0 | 0 | 1 | 3 | 0 | 23 | 11 | - |
| `AlundraFlailBounceWallProbeTests.FIronWk40_AWalkabilityBit0x40AtHeight0_BouncesAtTick10` | 50 | 0 | 0 | 1 | 3 | 0 | 23 | 11 | - |
| `AlundraFlailBounceWallProbeTests.FSteelWall2_TheSteelFlailBouncesTo0x25AtTick10` | 60 | 0 | 0 | 1 | 3 | 0 | 23 | 11 | - |
| `AlundraHeightTargetSitesTests.TheWaitOfTheBinaryEndsAtTheCallOfTheBinaryAtEveryRealSite` | 15547 | 27 | 0 | 19 | 99 | 0 | 491 | 104 | - |
| `AlundraHeroAttackTests.RealPrefab_TheHeroOfTheExport_SwingsTheBasicSwordOnTheGroundAndInTheAir_AtTheTicksOfTheOracle` | 43 | 0 | 0 | 1 | 3 | 0 | 19 | 2 | - |
| `AlundraHeroAttackTests.RealPrefab_TheTablesOfTheWorld_AreNotNull_AndASteelEdgePicksItsOwnRecord` | 3 | 0 | 0 | 1 | 3 | 0 | 19 | 2 | - |
| `AlundraHeroAuraWorldTests.AFrameHoldingTheTicks96And97AfterACreation_ShowsOneFrameWithoutAura_TheNextOneHasIt` | 182 | 46 | 0 | 1 | 4 | 0 | 23 | 11 | - |
| `AlundraHeroAuraWorldTests.AFrameOfThreeTicks_HoldingTheCreationOfTheAura_GivesTheSameStateAsFramesOfOneTick` | 300 | 0 | 0 | 6 | 24 | 0 | 138 | 66 | - |
| `AlundraHeroAuraWorldTests.AFrameOfTwoThenThreeTicks_OverTheCounterPassingFrom10To12_GivesTheSameStateAsFramesOfOneTick` | 100 | 0 | 0 | 2 | 8 | 0 | 46 | 22 | - |
| `AlundraHeroAuraWorldTests.TheAuraQuads_AreSortedStrictlyAfterTheHeroSprite_InTheSameBucket` | 60 | 0 | 0 | 1 | 4 | 0 | 23 | 11 | - |
| `AlundraHeroAuraWorldTests.TheHeroAnchor_ExposesTheKeyOfTheSortDriver_OnTheTicksOfTheSwingAndOfTheCharge` | 100 | 4 | 0 | 1 | 3 | 0 | 23 | 11 | - |
| `AlundraHeroAuraWorldTests.TheRenewalInFramesOfThreeTicks_WhereASparkTakesTheFreedSlot_StillGivesALiveAura` | 144 | 26 | 0 | 1 | 4 | 0 | 23 | 11 | - |
| `AlundraHeroChargeArenaTests.TheChargedSwing0x18_OfTheSteelSword_TouchesABeannoidOfMap14` | 150 | 24 | 0 | 1 | 3 | 0 | 24 | 11 | - |
| `AlundraHeroChargeArenaTests.TheEffectsOfTheCharge_AreCreatedInTheWorldsReservoir_TheBurstAtTheRelease` | 105 | 6 | 0 | 1 | 3 | 0 | 23 | 11 | - |
| `AlundraHeroChargeArenaTests.TheReservoirAfterTheChargeAndTheRelease_DoesNotDependOnTheNumberOfTicksPerFrame` | 424 | 24 | 0 | 4 | 16 | 0 | 92 | 44 | - |
| `AlundraHeroFallAndPadJumpTests.S6c_TheAdoptionOfANewPawnInTheAir_GivesTheEngineValuesBack` | 5 | 0 | 0 | 1 | 3 | 0 | 10 | 1 | - |
| `AlundraHeroJumpArcTests.A10J_TheHeroJumpsTheCliffOfMap10_TheWaterSlowsHim_GilesJumpsToo_TheSceneLeavesFor135` | 387 | 0 | 0 | 1 | 3 | 0 | 18 | 2 | - |
| `AlundraHeroLifeArcTests.ALoadAt4Over12_SurvivesTheFirstTick_AndTwoMapsInARow_KeepTheStats` | 60 | 0 | 0 | 2 | 6 | 0 | 33 | 12 | - |
| `AlundraHeroLifeArcTests.E3_TheHeroIsCreatedFromTheStats_AndTheStatsSurviveTheFirstTick` | 4 | 0 | 0 | 4 | 12 | 0 | 92 | 44 | - |
| `AlundraHeroLifeArcTests.TheAdoption_ComputesTheSheetAndTheBootsAtOnce_WithTheNewGameKit` | 0 | 0 | 0 | 1 | 3 | 0 | 19 | 11 | - |
| `AlundraHeroLifeArcTests.TheAdoption_DoesNotRewriteTheEquippedAccessory_TheFirstTickDoes_AndUsesTheOldId` | 1 | 0 | 0 | 1 | 3 | 0 | 10 | 1 | - |
| `AlundraHeroRunArenaTests.G7_RunningIntoABeannoid_OnTheRealWorld_IsTheOracle_TheBodyTouchesAtTick60` | 70 | 9 | 0 | 1 | 3 | 0 | 23 | 11 | - |
| `AlundraHeroRunArenaTests.G7_TheSweepOfThePressTick_OnTheRealWorld_IsTheOracleUntilTheHurt` | 1366 | 270 | 0 | 17 | 51 | 0 | 391 | 187 | - |
| `AlundraHeroRunArenaTests.G7_TheTackle_OnTheRealWorld_HitsTheBeannoidAtTick61_ThreeSparks_ComparedUntilTheHurt` | 74 | 18 | 0 | 1 | 3 | 0 | 23 | 11 | - |
| `AlundraHeroSlideTests.ArcsWithoutSlide_AnArcOfTheRealControllerThatSlid_FailsAtItsDispose_ThePinnedArcsPinTheirCount` | 3 | 0 | 0 | 3 | 9 | 0 | 30 | 3 | - |
| `AlundraHeroSwimArenaTests.TheHeroFallsFromTheBank_Floats_Swims_Bursts_AndStopsAgainstTheBank` | 420 | 0 | 0 | 2 | 6 | 0 | 46 | 22 | - |
| `AlundraHeroSwimArenaTests.WithoutTheBoots_TheHeroSwimsToo_AnAcceptedDeviation_O_E19_22` | 260 | 0 | 0 | 2 | 6 | 0 | 46 | 22 | - |
| `AlundraHitEffectWorldTests.W1_AHitOfTheNibble6Or7_AttachesTheEffect4Or5_ToTheHero_ForItsDuration` | 426 | 27 | 0 | 3 | 12 | 0 | 69 | 33 | - |
| `AlundraHitEffectWorldTests.W2_TheEffectDrawsNoRandomWord` | 284 | 18 | 0 | 2 | 8 | 0 | 46 | 22 | - |
| `AlundraHitEffectWorldTests.W3_TheSlotExists_InEveryGrouping_AtTheSameHitTick` | 852 | 54 | 0 | 6 | 24 | 0 | 138 | 66 | - |
| `AlundraHitPassCatchUpTests.ASwitchAskedAfterTheClockOfATick_IsTheSwitchTickOfTheNextOne_TheByteIsLoadedThereBeforeTheHitPass` | 100 | 9 | 0 | 2 | 6 | 0 | 46 | 22 | - |
| `AlundraHitPassCatchUpTests.ATargetThatEntersAnAnimationWithTheByte0x40_AtTheTickOfTheFirstOverlap_IsNotHit` | 100 | 9 | 0 | 2 | 6 | 0 | 46 | 22 | - |
| `AlundraHitPassCatchUpTests.ATargetThatLeavesAnAnimationWithTheByte0x40_AtTheTickOfTheFirstOverlap_IsHit` | 50 | 9 | 0 | 1 | 3 | 0 | 23 | 11 | - |
| `AlundraHitPassCatchUpTests.H1_WithThreeAndFourTicksPerFrame_IsTheOracleTickByTick` | 150 | 27 | 0 | 3 | 9 | 0 | 69 | 33 | - |
| `AlundraHitPassCatchUpTests.H1_WithTwoTicksPerFrame_AfterAFrameOfOneTick_IsTheOracleTickByTick` | 50 | 9 | 0 | 1 | 3 | 0 | 23 | 11 | - |
| `AlundraHitPassCatchUpTests.H1_WithTwoTicksPerFrame_ThePressOnTheFirstTickOfAFrame_IsTheOracleTickByTick` | 50 | 9 | 0 | 1 | 3 | 0 | 23 | 11 | - |
| `AlundraHitPassCatchUpTests.H1near_WithTwoTicksPerFrame_AfterAFrameOfOneTick_IsTheOracleTickByTick` | 50 | 9 | 0 | 1 | 3 | 0 | 23 | 11 | - |
| `AlundraHitPassCatchUpTests.H1near_WithTwoTicksPerFrame_ThePressOnTheFirstTickOfAFrame_IsTheOracleTickByTick` | 50 | 9 | 0 | 1 | 3 | 0 | 23 | 11 | - |
| `AlundraHitPassCatchUpTests.TheBoxCursor_FollowsTheSpritesLogicalClock_AfterEveryFrame_WhateverTheNumberOfTicks` | 43 | 9 | 0 | 1 | 3 | 0 | 23 | 11 | - |
| `AlundraHitPassCatchUpTests.TheHitWrittenAtTickN_IsReadByTheProgramsOfTickNPlus1_AndClearedByTheCounters_InTheSameFrameOfTwoTicks` | 50 | 9 | 0 | 1 | 3 | 0 | 23 | 11 | - |
| `AlundraHitPassCatchUpTests.TheSameByte0x40Test_InAFrameOfTwoTicks_StillSeesTheSwitchOfTheTickItself` | 100 | 9 | 0 | 2 | 6 | 0 | 46 | 22 | - |
| `AlundraHitPassTests.ARealArcOnMap14_TheHeroStrikesTheBeannoidOfRecord0_WhereTheMapPutItOnItsHill_SparksATouchAndTheProgramDAtTheNextTick_NoWound` | 22 | 9 | 0 | 1 | 3 | 0 | 23 | 11 | - |
| `AlundraHitPassTests.H1_TheBasicSword_HitsABeannoid40PixelsAwayAtTick5_ThreeSparks_ThenTheCooldown_ThenD` | 50 | 9 | 0 | 1 | 3 | 0 | 23 | 11 | - |
| `AlundraHitPassTests.H1_TheThreeSparksOfTheHitExistOnTheReservoir_AtTheCentreOfTheOverlap_Effect9` | 15 | 9 | 0 | 1 | 3 | 0 | 23 | 11 | - |
| `AlundraHitPassTests.H1both_TheSwingAndTheBodyOfABeannoid20PixelsAway_HitEachOtherAtTick1_SixSparks` | 50 | 36 | 0 | 1 | 3 | 0 | 23 | 11 | - |
| `AlundraHitPassTests.H1edge_ABeannoid59PixelsAway_IsHitAtTick5_ByTheLastPixelOfTheReach` | 50 | 9 | 0 | 1 | 3 | 0 | 23 | 11 | - |
| `AlundraHitPassTests.H1near_TheBoxOfTheFirstFrame_OfTheSwingThatStartsAtThisTick_HitsOnThePressTick_TheLatchIsThe0x10OfTheTick` | 50 | 9 | 0 | 1 | 3 | 0 | 23 | 11 | - |
| `AlundraHitPassTests.H2_ABeannoid60PixelsAway_IsNeverHit` | 50 | 0 | 0 | 1 | 3 | 0 | 23 | 11 | - |
| `AlundraHitPassTests.H3_TheHeroWalkingIntoABeannoid_IsTouchedAtTick17_TheCountersClearItAtTick18_TheHeroDoesNotLoseLifeYet` | 55 | 9 | 0 | 1 | 3 | 0 | 23 | 11 | - |
| `AlundraHitPassTests.H4a_ASecondSwing_HitsAgain25TicksAfterTheFirstHit_NotBefore` | 70 | 18 | 0 | 1 | 3 | 0 | 23 | 11 | - |
| `AlundraHitPassTests.H4b_ATargetInAnAnimationWithTheByte0x40_IsNeverHit_ByTheBoxThatOverlapsItForSevenTicks` | 50 | 0 | 0 | 1 | 3 | 0 | 23 | 11 | - |
| `AlundraHitPassTests.H5_TwoTargetsInTheReach_AreBothHitAtTheSameTick_TheHeroCountsTwoHits` | 50 | 18 | 0 | 1 | 3 | 0 | 23 | 11 | - |
| `AlundraHitPassTests.H6_AnImmuneTarget_GetsTheCooldownAndSparks_ButNoTouchingEntityAndNoProgramD` | 50 | 9 | 0 | 1 | 3 | 0 | 23 | 11 | - |
| `AlundraHitPassTests.H7_TwoAttackersOnOneTarget_OnlyTheFirstOfTheListTouchesIt_TheSecondHitsItWhenTheCooldownIsOver` | 50 | 27 | 0 | 1 | 3 | 0 | 23 | 11 | - |
| `AlundraHitPassTests.H8_TheSwingToTheWest_HitsABeannoid40PixelsAwayAtTick5` | 50 | 9 | 0 | 1 | 3 | 0 | 23 | 11 | - |
| `AlundraHitPassTests.TheReachBehindTheHero_Beyond9Pixels_IsOnlyContactedByTheLateFrames_BecauseTheHeroIsNotHurtYet` | 32 | 27 | 0 | 1 | 3 | 0 | 23 | 11 | - |
| `AlundraHitPassTests.TheReachInX_AtDyZero_IsTheOracleOne_FromMinus9To59Pixels` | 384 | 144 | 0 | 12 | 36 | 0 | 276 | 132 | - |
| `AlundraHitPassTests.TheReachInY_AtTheEdgesOfTheOracleWindows_IsTheOracleOne` | 480 | 81 | 0 | 15 | 45 | 0 | 345 | 165 | - |
| `AlundraHitPassTests.WhileTheContactLasts_ABeannoidTouchesTheHero_EveryTwentyFiveTicks_AndHisDamagedCounterStaysAtZero` | 90 | 45 | 0 | 1 | 3 | 0 | 23 | 11 | - |
| `AlundraInoaAwakeningArcTests.A8_TheAwakeningOnMap163_JessWalksToTheWall_TheHeroTakesBackTheHand` | 2447 | 0 | 0 | 1 | 3 | 0 | 18 | 8 | - |
| `AlundraInoaDayOneArcTests.A10_TheFirstVisitOnMap165_TheActorsPassTheSceneOnByFlags_ThePlayerGetsTheHandBack` | 1777 | 0 | 0 | 1 | 3 | 0 | 9 | 1 | - |
| `AlundraInoaDayOneArcTests.A11_SeptimusOnMap164_TheScenePlaysToItsEnd_TheTableAndTheFlagsAreSet` | 2075 | 0 | 0 | 1 | 3 | 0 | 13 | 7 | - |
| `AlundraLeverArcTests.A_Map115_TheNorthWestLever_TurnsAndOpensTheDoorAtTheTicksOfItsPrograms` | 105 | 9 | 0 | 1 | 3 | 0 | 4 | 3 | - |
| `AlundraLeverBasculeArcTests.C_Map117_TheBascule_RewritesThePlatformsAtTheTicksOfItsProgram_InFourHits` | 136 | 36 | 0 | 1 | 3 | 0 | 3 | 3 | - |
| `AlundraLeverBridgeArcTests.B_Map116_TheTwoLeversExtendTheBridge_AndTheSecondHitOnTheLever40RetractsIt` | 143 | 27 | 0 | 1 | 3 | 0 | 27 | 15 | - |
| `AlundraLeverDoorsArcTests.E_Map44_TheLever7_TurnsTheSixLevers_WalksTheSevenDoors_AndGivesTheHandBack` | 110 | 9 | 0 | 1 | 3 | 0 | 21 | 6 | - |
| `AlundraLeverDoorsArcTests.E_Map44_WithoutG1633_TheHitOnlyRunsTheTestAndTheEnd_NoLockNoT80` | 48 | 9 | 0 | 1 | 3 | 0 | 21 | 6 | - |
| `AlundraLeverPillarArcTests.D_Map117_TheLever22_StartsThePillar_AtTheTicksOfItsPrograms` | 107 | 9 | 0 | 1 | 3 | 0 | 5 | 4 | - |
| `AlundraLeverSmokeTests.Map115_TheNorthWestLeverIsThere_AndTheTwoRecordsThatNeverAppearAreNot` | 6 | 0 | 0 | 1 | 3 | 0 | 4 | 3 | - |
| `AlundraLeverSmokeTests.Map116_TheTwoLeversAreThere_AndThePlatesAreNot` | 6 | 0 | 0 | 1 | 3 | 0 | 27 | 15 | - |
| `AlundraLeverSmokeTests.Map117_TheBasculeRoomHasTheBascule_AndNotTheLever22` | 6 | 0 | 0 | 1 | 3 | 0 | 3 | 3 | - |
| `AlundraLeverSmokeTests.Map117_TheLever22RoomHasTheLeverAndThePillar_AndNotTheBascule` | 6 | 0 | 0 | 1 | 3 | 0 | 5 | 4 | - |
| `AlundraLeverSmokeTests.Map44_TheSixLeversAreThere_WithG1633` | 6 | 0 | 0 | 1 | 3 | 0 | 21 | 6 | - |
| `AlundraLoadingMapAirArrivalTests.V1b_AnArrivalHighAboveTheWell_FallsIn0x2D_FromItsFirstFrameInTheAir_ThenIdleAfterTheLanding` | 60 | 0 | 0 | 1 | 3 | 0 | 3 | 1 | - |
| `AlundraLoadingMapAirArrivalTests.V1c_AnArrivalOnTheGround_KeepsTheArrivalAnimationUntilItsChain_ThenIdle` | 30 | 0 | 0 | 1 | 3 | 0 | 19 | 2 | - |
| `AlundraLogicalZStepTests.A_TheMarker_IsPosedByTheInterfaceAndByItAlone_NotByTheHero_TheRecordCreations_TheRecycledEntities_NorTheClone` | 0 | 0 | 0 | 1 | 3 | 0 | 20 | 11 | - |
| `AlundraLogicalZStepTests.B1_TheBall_FollowsTheSeriesOfTheOracleMinusOneRawUnit_LandsAtTheTickOfTheOracle_AndTheSameBallWithoutGravityStaysAtItsHeight` | 0 | 0 | 0 | 1 | 3 | 0 | 21 | 11 | - |
| `AlundraLogicalZStepTests.B2_TheArrow_KeepsItsZ_IsOnGroundIsZeroWithTheRawUnitAndOneWithout_AndTheBallOnTheSameFloorLandsOnIt` | 0 | 0 | 0 | 1 | 3 | 0 | 22 | 11 | - |
| `AlundraLogicalZStepTests.B3_TheImpulseOf1024_FollowsTheSeriesOfTheCrateMinusOneRawUnit_AndLandsAtTheTickOfTheOracle_WhereTheSameEntityWithoutImpulseOnlyFalls` | 0 | 0 | 0 | 1 | 3 | 0 | 21 | 11 | - |
| `AlundraLogicalZStepTests.B4_TheExactGraze_DoesNotLandAtOnce_AndLandsAtTheNextTick_WhereAGrazeOneRawUnitDeeperLandsAtOnce` | 0 | 0 | 0 | 1 | 3 | 0 | 21 | 11 | - |
| `AlundraLogicalZStepTests.B5_TheCeiling_StopsTheRiseAtTheAbsoluteCeiling_WithTheContactAndTheForceKeptWithoutGravity_ClearedWithIt_AndALowerRiseGoesOn` | 0 | 0 | 0 | 1 | 3 | 0 | 22 | 11 | - |
| `AlundraLogicalZStepTests.B6_TheMagnet_SnapsAtMostThreePixelsAboveAndThreePixelsPlusOneUnitBelow_OnlyWithGravityForceZeroAndAnAcceptedXyStep` | 0 | 0 | 0 | 1 | 3 | 0 | 20 | 11 | - |
| `AlundraLogicalZStepTests.B7_TheStopMarker0x8000_ZeroesTheForceWithoutGravity_WhereWithGravityItIsTheImpulseShiftedByEight` | 0 | 0 | 0 | 1 | 3 | 0 | 21 | 11 | - |
| `AlundraLogicalZStepTests.B8_TheStep_DeclaresTheSentinelAndThePositionTheRootFollows_ThroughAVerticalMoveOnly_AndTheHeroIsLeftAlone` | 0 | 0 | 0 | 1 | 3 | 0 | 20 | 11 | - |
| `AlundraLogicalZWorldTests.Z10_ATeleportInFlight_KeepsTheMarkedEntityInTheAirAgainstAnEngineUpdateWithoutATick_WhereTheRecordEntityIsSetBackOnTheGround` | 13 | 0 | 0 | 1 | 3 | 0 | 24 | 11 | - |
| `AlundraLogicalZWorldTests.Z11_TheMarkedEntity_KeepsTheTerrainHeightFieldAtItsPosition_TheForceOfTheLandingTick_AndTheLogicalGroundFlagAcrossAnEngineUpdate` | 90 | 0 | 0 | 4 | 12 | 0 | 96 | 44 | - |
| `AlundraLogicalZWorldTests.Z1_AnArrowBornAtTheHeightOfTheHero_KeepsHeroPlusOneRawUnitAndIsNotOnTheGroundFor30Ticks_WhereTheRecordEntityIsUnchanged` | 120 | 0 | 0 | 3 | 9 | 0 | 72 | 33 | - |
| `AlundraLogicalZWorldTests.Z2_AnArrowOver16PxIsNotOnTheGroundAtAnyTick_AndAgainst32PxItIsStoppedAtTheTickOfTheOracle` | 180 | 0 | 0 | 3 | 9 | 0 | 72 | 33 | - |
| `AlundraLogicalZWorldTests.Z3_TheBallAgainstAStepOf32Px_IsStoppedAtTheTickOfTheOracleAndFallsToTheGround_AndOnAStepOf16Px_ItIsPosedOnItAtTheTickOfTheOracle` | 28 | 0 | 0 | 2 | 6 | 0 | 48 | 22 | - |
| `AlundraLogicalZWorldTests.Z3_TheBall_FollowsTheSeriesOfTheOracleFromTheFirstTick_AndLandsAtTheTickOfTheOracle` | 36 | 0 | 0 | 3 | 9 | 0 | 72 | 33 | - |
| `AlundraLogicalZWorldTests.Z4_AnImpulseOf1024_LandsAtTheTickOfTheOracleFlatOver16PxAndOver32Px_OnTheSeriesOfTheCrateMinusOneRawUnit` | 65 | 0 | 0 | 3 | 9 | 0 | 72 | 33 | - |
| `AlundraLogicalZWorldTests.Z5_AFreezeAndAThawInTheAir_LetTheZGoOnOnTheSeriesOfTheOracle_AndNeverAnchorTheEntity_WhereTheRecordEntityIsUnchanged` | 25 | 0 | 0 | 1 | 3 | 0 | 25 | 11 | - |
| `AlundraLogicalZWorldTests.Z6_TheRootFollowsTheLogicalZToTheRawUnit_AndAnEngineUpdateWithoutATickDoesNotMoveIt` | 26 | 0 | 0 | 3 | 9 | 0 | 72 | 33 | - |
| `AlundraLogicalZWorldTests.Z7_TheStepHeight_IsZeroInTheAir_TheValueOfThePrefabAtRestWithGravity_AndZeroWithoutGravity` | 14 | 0 | 0 | 1 | 3 | 0 | 25 | 11 | - |
| `AlundraLogicalZWorldTests.Z8_TheBallAndTheFlushArrow_HaveTheSameStateAtTicks6And12_Whatever1_2Or3TicksPerFrame_AndItIsTheOracles` | 36 | 0 | 0 | 3 | 9 | 0 | 75 | 33 | - |
| `AlundraLogicalZWorldTests.Z9_TheMagnetOfTheBinary_PosesAWalkerOnTheLowerGroundWithin3Px_AndLeavesItInTheAirBeyond_OnTheSeriesOfTheOracle` | 80 | 0 | 0 | 2 | 6 | 0 | 48 | 24 | - |
| `AlundraMapEdgeWorldTests.B1_AnArrowBornAtX1200_IsBoundedAtTheTickOfTheOracleWithTheFlagTheSameTick_AndAnArrowBornAtX1100IsNotBoundedFor20Ticks` | 20 | 0 | 0 | 1 | 3 | 0 | 25 | 11 | - |
| `AlundraMapEdgeWorldTests.B2_AWalkerPushedEast_IsBoundedAtTheTickOfTheOracleAndStaysAtTheBound_WithTheFlagUpAtTheTickAfterToo` | 8 | 0 | 0 | 1 | 3 | 0 | 24 | 12 | - |
| `AlundraMapEdgeWorldTests.B2_AWalkerPushedNorthEast_IsBoundedOnXWhileYGoesOn_AndTheFlagIsKeptThroughTheSlideAlongTheWall` | 11 | 0 | 0 | 1 | 3 | 0 | 24 | 12 | - |
| `AlundraMapEdgeWorldTests.B3_ARecordWalkerAtTheEdgeIsNotBounded_WhereTheMarkedArrowIsBounded` | 14 | 0 | 0 | 1 | 3 | 0 | 24 | 11 | - |
| `AlundraMapEdgeWorldTests.B4_TheHeroPushedEast_GoesPastItsOwnBoundAtTheTickItsSpeedGives_WhereTheMarkedArrowIsBounded` | 28 | 0 | 0 | 1 | 3 | 0 | 24 | 11 | - |
| `AlundraMapStateTests.P3_AMarkedEntityWhoseFirstAnimationIsValidated_KeysItsKinematicsOnItsCurrentAnimation_NotOnAnAskedOne` | 3 | 0 | 0 | 1 | 3 | 0 | 27 | 11 | - |
| `AlundraMapStateWorldTests.K1_AnArrowBornWithTheMap_StopsAtTheWallOfTheMaskAtTheXOfTheOracle_AndTheSameArrowInAFreeCorridorFliesTheWholeDistance` | 120 | 0 | 0 | 2 | 6 | 0 | 48 | 22 | - |
| `AlundraMapStateWorldTests.K2_ABallBornWithTheMap_FallsAndLandsAtTheTickOfTheOracle_AndTheSameBallWithoutAMapStaysInTheAir` | 32 | 0 | 0 | 2 | 6 | 0 | 48 | 22 | - |
| `AlundraMapStateWorldTests.K3_OnMap159_TheBallBarelyFalls_WhereOnMap14_ItHasLandedAtTheSameTick` | 22 | 0 | 0 | 2 | 6 | 0 | 48 | 22 | - |
| `AlundraMapStateWorldTests.K4_TheFieldsOfTheEntityBornByTheInterface_AreTheOnesOfAnEntityOfTheSameSpriteCreatedFromARecord` | 0 | 0 | 0 | 1 | 3 | 0 | 20 | 11 | - |
| `AlundraPrefabArcSupportTests.ArcRun_HeldDirections_PersistOverFrames_AndOnlyTheFirstFrameIsJustPressed` | 5 | 0 | 0 | 1 | 3 | 0 | 16 | 12 | - |
| `AlundraPrefabArcSupportTests.TH3b_ArrivalByPortal162_3_OnMap165_TheAdoptionRaisesTheHeroOntoTheGroundOfTheTile` | 10 | 0 | 0 | 1 | 3 | 0 | 9 | 1 | - |
| `AlundraRandomPerTickTests.TheChargeSparks_DrawBeforeTheAdvanceOfTheirTick_WhateverTheGrouping` | 318 | 18 | 0 | 3 | 12 | 0 | 69 | 33 | - |
| `AlundraShipArcTests.A0_TheCaptainWalksTheStairs_TheHeroStaysPut_ThenTheControlComesBack` | 126 | 0 | 0 | 1 | 3 | 0 | 16 | 12 | - |
| `AlundraShipArcTests.A0b_TheHeroInThePushZone_IsWalkedAside_ThenTheCaptainLeavesAndTheControlComesBack` | 160 | 0 | 0 | 1 | 3 | 0 | 16 | 12 | - |
| `AlundraShipArcTests.A1_TheCabin_TheDialogueCloses_TheHeroSleeps_ThenTheWarpToMap476Departs` | 386 | 0 | 0 | 1 | 3 | 0 | 16 | 12 | - |
| `AlundraShipArcTests.A1c_TheCabinWithARealController_TheSecondNorthWalkReachesItsContact_ThenTheWarpToMap476Departs` | 387 | 0 | 0 | 1 | 3 | 0 | 16 | 12 | - |
| `AlundraShipCorridorArcTests.A5_TheHeroTakesTheHand_IsPlacedInTheCorridor_ThenTheDownPushedPortalLeadsToMap391` | 215 | 0 | 0 | 1 | 3 | 0 | 12 | 12 | - |
| `AlundraShipCorridorArcTests.A5r_HoldingDownInPlace_TheShipRollsTheHeroOnce_ThenTheDownPushedPortalLeadsToMap391` | 238 | 1 | 0 | 1 | 3 | 0 | 12 | 12 | - |
| `AlundraShotDirectionTests.D1_InTheAir_SquarePlusLeftAtTickP_ShotsAtPPlus1_ToTheWest_At1TickAnd2TicksPerFrame` | 48 | 0 | 0 | 2 | 6 | 0 | 46 | 22 | - |
| `AlundraShotDirectionTests.D2_InTheAir_SquareAtTickP_LeftOnlyFromTheShotTick_ShotsToTheEast_TheTurnOfTheShotTickIsIgnored` | 24 | 0 | 0 | 1 | 3 | 0 | 23 | 11 | - |
| `AlundraShotDirectionTests.D3_OnTheGround_SquarePlusADirection_ShotsInThatDirection` | 104 | 0 | 0 | 4 | 12 | 0 | 92 | 44 | - |
| `AlundraShotDirectionTests.D4_TheBoxDirection_EqualsTheFacingOfTheHeadOfTheTick_AtTheTickOfTheShot` | 408 | 0 | 0 | 12 | 36 | 0 | 276 | 132 | - |
| `AlundraSpawnBySpriteIndexInterfaceTests.B2_TheFullPath_On151NumbersOfTheGlobalPage_AndTheNumbersOfTheMapPageValidOnMap14_GivesTheProjectionOfTheOracle_AndAppendsToTheWorld` | 0 | 0 | 0 | 1 | 3 | 0 | 175 | 20 | - |
| `AlundraSpawnBySpriteIndexInterfaceTests.C_TheNullContract_AnInvalidIndexAnAbsentPrefabOrAnUnknownMap_AddsNothing_AndLogsOneLine` | 0 | 0 | 0 | 1 | 3 | 0 | 20 | 11 | - |
| `AlundraSpawnBySpriteIndexInterfaceTests.D_TheNewEntity_IsNotFoundBy0x2COr0x2EOf0_AndItsTriggerIsMinusOne` | 0 | 0 | 0 | 1 | 3 | 0 | 20 | 11 | - |
| `AlundraSpawnBySpriteIndexInterfaceTests.E_TheParentAndTheRoot_ThroughTheInterface_FourCases` | 0 | 0 | 0 | 1 | 3 | 0 | 23 | 11 | - |
| `AlundraSpawnBySpriteIndexInterfaceTests.F_TheEightCallsOfTheAt9Traces_AppearAtTheZOfTheBinaryMinusOneRawUnit_UnlessTheTerrainRaisesThem` | 0 | 0 | 0 | 1 | 3 | 0 | 27 | 11 | - |
| `AlundraSpawnBySpriteIndexInterfaceTests.F_TheRawUnitOfZ_IsAddedByTheInterface_AHeroZMakesAnEntityOneRawUnitHigher` | 0 | 0 | 0 | 1 | 3 | 0 | 21 | 11 | - |
| `AlundraSpawnBySpriteIndexInterfaceTests.G_TheSoundOfTheAnimation0_IsRequestedOnceAtTheTickOfTheCall_AndOnlyWhereTheBinaryPlaysOne` | 0 | 0 | 0 | 1 | 3 | 0 | 22 | 11 | - |
| `AlundraSpawnBySpriteIndexInterfaceTests.H_TheNumber79OfTheMapPage_ByTheInterface_AndTheRecord0OfMap14_ByTheProductionPath_DifferOnlyByTheClosedList` | 0 | 0 | 0 | 1 | 3 | 0 | 20 | 12 | - |
| `AlundraSpawnBySpriteIndexInterfaceTests.L_TheIndicesOfTheMapPage_AreReadAtTheFirstCallOfTheMapPage_NeverAtTheLoadOfAWorld` | 0 | 0 | 0 | 1 | 3 | 0 | 21 | 12 | - |
| `AlundraStoredHitBoxTests.TheWorldsTick_StoresTheBoxOfAnEntityWithNoAttack_AfterThePhysics` | 11 | 9 | 0 | 1 | 3 | 0 | 23 | 11 | - |
| `AlundraTarnMurggArcTests.R32_TheMurggsOfTarnsManor_TheScriptsEnd_TheCorpseOfRecord2IsRecycled_TheControlComesBack` | 308 | 0 | 0 | 1 | 3 | 0 | 24 | 14 | - |
| `AlundraVisionAndCoastArcTests.A9_WendellOnMap172_TheWaitOnALoopSeesItsTurn_ThePlayerGetsTheHandBack` | 103 | 0 | 0 | 1 | 3 | 0 | 11 | 4 | - |
| `AlundraWaveSwordLifeTests.V1_E10_DestroysTheWaveAtSPlus26_TheSlotIsFreeAtSPlus27AndTakenAgain_NoBreakEffect_WhateverTheNativeSwitch` | 310 | 28 | 0 | 2 | 6 | 0 | 50 | 22 | - |
| `AlundraWaveSwordLifeTests.V2_C61_CallsTheNamedStubOnceAtEachTickOfSPlus1ToSPlus25_AndNeverWithTheNativeSwitchOff` | 936 | 86 | 0 | 6 | 18 | 0 | 144 | 66 | - |
| `AlundraWaveSwordLifeTests.V4_TheWholeLifeOfTheWave_FromSToSPlus27_EqualsTheKeysOfTheOracle_WhateverTheGrouping` | 2808 | 108 | 0 | 18 | 54 | 0 | 432 | 198 | - |
| `AlundraWaveSwordWiringTests.P1_R_TheChargeOfTheSword_BirthsTheWaveOfItsType_DaughterOfTheHero_AtTheLowestFreeSlot_AndItsLifeEqualsTheKeys` | 620 | 24 | 0 | 4 | 12 | 0 | 96 | 44 | - |
| `AlundraWaveSwordWiringTests.P2_R_TheWaveTouchesTheTargetsBeyondTheHeroReach_AtSPlus5_7_9_AndTheHeroTheOneInFrontOfHim` | 620 | 69 | 0 | 4 | 12 | 0 | 96 | 44 | - |
| `AlundraWaveSwordWiringTests.P3_R_TheWaveNeverTouchesTheHero_InTheSameRunWhereTheTargetIsTouchedByTheWave` | 155 | 15 | 0 | 1 | 3 | 0 | 24 | 11 | - |
| `AlundraWaveSwordWiringTests.P4_R_TheWaveOfProductionExistsInTheChargeArena_DiesAtSPlus26_AndThePinOfTheArenaStaysAtTwoContacts` | 150 | 24 | 0 | 1 | 3 | 0 | 24 | 11 | - |
| `AlundraWaveSwordWiringTests.P5_R_TheBowShotCreatesNothingUnderTheSetOfTheSwords_TheSwordShotWithTheProductionSetCreatesOneEntity` | 4 | 0 | 0 | 2 | 6 | 0 | 47 | 22 | - |
| `AlundraWaveSwordWiringTests.P6_R_WithNoFreeSlotTheShotOfTheWaveCreatesNothingAndClearsThePointer_WithExactlyOneFreeSlotItCreatesOneEntity` | 4 | 0 | 0 | 2 | 6 | 0 | 126 | 22 | - |
| `AlundraWaveSwordWiringTests.P7b_R_TheTouchingEntityOfTheTarget_IsTheWaveOfProduction_AtTheTickOfTheTouch_WithItsAttack` | 155 | 15 | 0 | 1 | 3 | 0 | 24 | 11 | - |
| `AlundraWeaponSpawnSeamTests.S1_TheEmptySet_CountsAndJournalsTheShotAndCreatesNothing_TheSetWithTheArrow_CreatesOneEntityInTheLowestFreeSlot` | 4 | 0 | 0 | 2 | 6 | 0 | 47 | 22 | - |
| `AlundraWeaponSpawnSeamTests.S2_WithTheHook_TheHooksEntityIsTheOnlyOne_WithoutTheHook_TheProductionCallMakesOne` | 4 | 0 | 0 | 2 | 6 | 0 | 48 | 22 | - |
| `AlundraWeaponSpawnSeamTests.S3_OnAMontageHost_TheShotIsCountedAndJournalled_NoInterfaceIsCalled_AndTheSameShotInTheWorldCreatesOne` | 2 | 0 | 0 | 1 | 3 | 0 | 24 | 11 | - |
| `AlundraWeaponSpawnSeamTests.S4_WithAFullTable_NothingIsCreatedAndThePointerIsCleared_WithOneFreeSlot_OneEntityIsCreated` | 4 | 0 | 0 | 2 | 6 | 0 | 126 | 22 | - |
| `AlundraWeaponSpawnSeamTests.S6_ADirectionOutsideTheTable_IsClampedToTheNearestEntry_AndANormalDirectionIsUnchanged` | 4 | 0 | 0 | 4 | 12 | 0 | 92 | 44 | - |
| `AlundraWeaponSpawnSeamTests.S7_TheCounterAndTheJournalAreAlreadyWritten_WhenTheHookIsCalled_AndTheHookIsCalledOnce` | 2 | 0 | 0 | 1 | 3 | 0 | 23 | 11 | - |

### The tests marked `ambient_draws` (T1 handling: written justification, no seed assignment added, so the shadow was not rerun)

Sixteen tests of the list have `ambient_draws > 0`, the same as in the e0b1 run (draws attributed to the test before any epoch of THAT test). None needs an explicit seed assignment, and none can be moved by the loot roll (their `divergent_draws`
are 0 and they create nothing but `ArcRun_ResetsTheSharedRandomStream_...`, which reads the seed only after the construction):

- **Classes that reset the stream in their constructor** (the constructor has its own key, so its `Reset()` is an epoch of `Class..ctor`, not of the method): `AlundraRandomTests` (3 tests), `AlundraRandomOpcodeTests` (1), `AlundraRandomBranchOpcodeTests` (3),
  `AlundraNativeEffectTests` (3). Their start state is the initial seed whatever the order of the tests.
- **Montages of the hero without a world `Update`** (`AlundraHeroSwimTests.W6_...` x2, `AlundraHeroRunTests.G7_TheSacredSword...`, `AlundraHeroAuraTests.AHeldCharge...`): the charge sparks draw their jitter from the inherited state; no assertion reads a spark.
- **`AlundraStoryChainSkippedOpcodesTests.Oracle_AnOpcodeTheRunnerHandlesIsPorted_...`** (2 ambient draws, a key that was not in the e0b1 list, where `AlundraWeaponLockOpcodesTests.TheWholeProgramB_...` was): the test asks whether opcodes are ported
  (`IsPorted(...)`), which runs the handlers on a throw-away entity; it asserts booleans only and reads no drawn number.
- **`AlundraPrefabArcSupportTests.ArcRun_ResetsTheSharedRandomStream_AtConstructionAndAtDispose`**: it builds arcs on map 390 (spec `RandomReset`: the load rolls 12 steps in the binary, 16 creations seen), seeds the stream itself and reads it right after the construction: it reads the word AFTER the second re-seed of `ArcRun`,
  which is the initial seed in both phases (this is why Q-B1 re-seeds after the load: without it, phase B would fail this test on map 390).

## T1 evidence: red before the code, green after

**`AlundraLootRollTests` (8 tests)**: written first and run on a skeleton that compiles (`Choose` and `Roll` answering 0, the table zero-filled): **7 red, 1 green** (the theory on the 256 codes `0x100..0x1FF` expects item 0 and no draw, which the skeleton
answers too; it bites on the real code through the mutants of T3 `code >= 0x100` and by the table lookups). With the real class all 8 are green (2 626 + 256 + 52 + 183 + 12 + 6 000 + 11 vectors, 0 mismatch; the table md5 equal).

**The tests of the real world, written after the phase A code, run on it (Release, 2026-10-16):**

| Test | On the phase A code | First difference |
|---|---|---|
| `M0_ThePairsOfV8_...` (20 pairs, the tile of each arrival) | green | - |
| `TheWordAfterTheLoad_IsTheSeed_OnTheMapsWhoseLoadRollsNothing_SevenOfTheTwentyPairs` | green (also green in phase B) | - |
| `WithoutTheVariableOfTheSink_TheHelperOfTheCreation_...` (inertness) | green (also green in phase B) | - |
| `M1_TheCreationByTheInterface_OnMap14_...` (9 vectors) | **red** | `0/230 seed 12345678: word 12345678`, expected `B806E07F` (and the item 84 of the seed `985CF02A` is 0) |
| `M2_TheMapLoad_ByTheLoadTileOfTheHero_...` (20 pairs) | **red at its SECOND assertion** | the first (tile and entities in order with their slots and sprites, 20 pairs) PASSES; the second (items, `+0x40`, word after the load) fails: `#0 map 14` has no item 71 on record 1 |
| `M3_TheFlagsOfTheRecord_FiveCases_...` | **red** | `R3 ... flag=0x0001 bit not set: item 0`, expected item 85 (the raised cases already agree: item 0, no draw) |
| `M4_AnAbortedCreation_...` | **red** | `aborted null, word 12345678` agrees; `second item 0 word 12345678`, expected `second item 0 word B806E07F` (one step) |
| `M5_TheHeroRollsNothing_...` | **red** | the hero part agrees (`item 0 +0x40 0 header 0`, word B017C93D after the load); the creation: word `12345678`, expected `B806E07F` |

No (m1) to (m5) is green before T2. They are kept OUT of the commit of phase A: `AlundraLootRollWorldTests.Rolls.cs` (a partial of the committed class, the rolling tests M1 to M5), uncommitted in the working tree for phase B;
a copy is in the scratch folder of the run.

**The STOP condition "the load loop of the DLL does not create, at the verified load tile, the same entities as V8" does not occur**: for the 20 pairs the DLL's load creates exactly V8's entities, in V8's slots, with V8's sprite numbers, at the
tile that `HeroTileAtLoad` reads (M2, first assertion).
