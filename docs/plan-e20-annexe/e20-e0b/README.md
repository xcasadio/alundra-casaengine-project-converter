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

(T2 to T4 add their measures below.)
