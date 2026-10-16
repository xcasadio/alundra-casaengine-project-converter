# E20.e0a5 - annex of the oracle and predicted lists (T0 of E20.e0a5)

Plan: `docs/plan-e20-heros.md`, section 1.16.5 (E20.e0a5, tasks T0 to T4). This folder holds the oracle of the slice (the clock of the animation and the box of the tick of birth of an entity born by the interface), the list of the tests
that create such an entity, and the tests of the existing suite that the slice is predicted to move. It is real code of `ALUN_CD.EXE` (France) executed by the emulated machine of the E14/E20 oracles (`oracle/tools/pworld.py`, the
f-projectiles machine: real `MovePlayer`, real `SpawnWarpEntity` `0x80039F58`, real `InitializeEntity` `0x80039D04` and its first switch `0x80038AB4`, real animation pass `0x80038E18`, real physics pass `0x80038364`, real hit pass;
effects, sounds and every other handler are journalling stubs). Nothing here knows an expected value of the DLL: every number of `out/birth_clock.json` is read from the machine's RAM.

## Run

Run from a SCRATCH COPY of the folder (the scripts write into `../out` relative to their own folder), foreground, about 40 seconds, `python -B`:

```
python -B oracle/run_e0a5.py json      # the rows of the clock and of the box -> out/birth_clock.json, out/birth_clock.txt, out/trace_wave_steel_east*.txt
python -B oracle/run_e0a5.py checks    # out/checks.txt (21 facts, PASS / FAIL; exit code 0 = all PASS), then out/manifest.txt (md5 of the other files of out/)
```

`checks` reads `../../e20-f3/oracle/out/wave_steel_east.json` (same relative layout as in the repository) to prove that the wave keys of E20.f3 (`cnt1c`, `reset_AC`, `hbx - px`) are the ones of this oracle.

## Determinism (T0 acceptance)

Two complete runs, each in its own scratch copy of the annex, gave an identical `out/manifest.txt` (12 files, md5) and `diff -rq` on the two `out/` folders was empty. The nine pass-by-pass traces
`out/trace_wave_steel_east*.txt` are identical (modulo the CR of the Windows text mode of the discovery) to the discovery's `critic/out` ones, `NOSWITCH` and the seven `SW*` variants included. The 21 facts of `out/checks.txt` are PASS: no row
contradicts the model of the plan.

## Rows of `out/birth_clock.json` and the keys the plan names

`rows[]` has one row per label; `ticks[]` has one entry per tick, `k` = tick - S, S = the tick of the creation (0 for the record-born entity of a map load, whose tick numbers are the absolute ones). Positions and sizes are raw 16.16 integers
(`bw bd bh` are the binary's size minus one raw unit: `(n << 16) - 1`).

| Row | What it is | Source | Keys |
|---|---|---|---|
| `WAVE` | the sword wave (global sprite 27, steel, east), born in `MovePlayer`, no script switch | `tools/trace_birth2.py` | `cnt1c`, `end_AC` (the `reset_AC` of `e20-f3`), `status`, `px py pz`, `hbx hby hbz`, `bw bd bh`, `loops_B0`, `frame_shown`, `cnt_A8` |
| `NOSWITCH` | the same with the `jal 0x80038AB4` of `InitializeEntity` replaced by a nop: the mutant that reproduces the DLL's lag | same, env `NOSWITCH` | same |
| `SW0` `SW1` `SW2` `SW5` | the animation 1 of the sprite 27 (16 ticks) asked at S+k, before the animation pass of the tick | same, env `SWITCH=k:1` | same |
| `SW1-2` | animation 1 at S+1, then back to 0 at S+2 | env `SWITCH=1:1,2:0` | same |
| `SWD1` `SWD2` | the direction word 16 asked at S+1 / S+2 (the animation stays 0) | env `SWITCH=k:0:16` | same |
| `SPR251` `SPR3` | the global sprite 251 (first image of 1 tick) and the arrow (3) born in `MovePlayer` | `tools/trace_kinds.py`, scenario `move`, `SPR` | `cnt1c`, `end_AC`, `loops_B0`, `frame`, `cnt_A8`, `adir` |
| `LOAD27` | the wave sprite born by a map record at load (real record spawner `0x8003A1B8` before tick 1) | `tools/trace_kinds.py`, scenario `load` | same |

Values of the table of the plan, as the oracle gives them (the tests read them BY KEY - `AlundraCreationSwitchClockTests` and the existing tests rewritten by derivation - and never paste them):

| Value | Row, key | Oracle |
|---|---|---|
| Call n. k, box cursor | `WAVE`, `cnt1c` | 1 at S, 2 at S+1, 3 at S+2, 4 at S+3 |
| End flag of the wave | `WAVE`, `end_AC` (and `e20-f3`, `reset_AC`) | 0 up to S+24, 1 at S+25 (status 4 at S+26) |
| Box of the wave | `WAVE`, `hbx - px`, `bw bd bh` | changes at S, S+1, S+3, S+5, S+7, S+9; -18 / -6 / +6 / +18 / +30 / +42 px; 36 x 24 x 31 |
| Mutant (the DLL's lag) | `NOSWITCH`, `end_AC`, box | flag at S+26, status 4 at S+27, box changes at S+2, S+4, ... |
| Switch in the window, first `end_AC` | `SW0` `SW1` `SW2` `SW5` `SW1-2` `SWD1` `SWD2` | S+16, S+17, S+18, S+21, S+28, S+27, S+28 |
| Box of the animation 1 before the integration | `SW0`, `hbx`, `bw..bh` | x 88 ; 23 x 23 x 15 |
| Loop turn, sprite 251 | `SPR251`, `loops_B0` | 1 at S+4, 2 at S+9 |
| Loop turn, arrow 3 | `SPR3`, `loops_B0` | 1 at S+11 |
| Witness of the deviation (record) | `LOAD27`, `end_AC` | 1 at tick 26 (call n. 1 at tick 1); the DLL: tick 27 |
| First counted tick, born in another entity's program | `e20-e0a/oracle/out/v6.json`, `BT-H*`, `delay` | 3 during tick 1, 2 after tick 2 (unchanged file) |
| `has_box` at the creation | `e20-e0a/oracle/out/v1.json`, `v2.json`, `p.has_box` | per vector (unchanged files, phase B) |

## Tools

`oracle/tools/*.py` are byte copies of the 77 files of the folder `critic/tools` of the discovery of 2026-10-16 (it holds the 56 files of the `e20-f3` annex plus `trace_birth.py`, `trace_birth2.py` - the `SWITCH` variable -, `trace_kinds.py` and the
E20.e0k machine files); `oracle/critic/*.py` are the 14 model scripts of the critic of the same discovery (`clockmodel.py` and `clockmodel2.py` model the clock of the DLL, INFERENCE and not a DLL run). `oracle/checksums.txt` has the SHA-1 of every
file with its origin ("= path" for the same bytes in an earlier annex, "NEW", "DIFFERENT"); `diff -rq` against the discovery folders was empty at the copy. `oracle/run_e0a5.py` is the only file written for this slice.

## The probe of the resolver (T0, item 4)

One fact for phase B: the animation assets of a prefab can be acquired BEFORE the engine integrates the entity, and they are the instances the sprite holds after it. Throwaway test (not committed), map 14, sprite 27 of the global page, born by
`SpawnEntityBySpriteIndex`: `AnimatedSpriteComponent.AnimationAssetIds` holds 8 ids, `Animations.Count` is 0 and `entity.World` is null before the integration; the 8 `AssetContentManager.Acquire<Animation2dData>(id)` taken before it and the 8 taken after
the next frame are the same instances (`ReferenceEquals`), and each is held by `sprite.Animations[i].Animation2dData`. Result: PASS, so the resolver of phase B may build the box timeline from `Animation2dData` before the integration. Source of the probe:

```csharp
#nullable enable
using System;
using System.Linq;
using Alundra.Scripts;
using CasaEngine.Framework.Assets.Animations;
using CasaEngine.Framework.Scene.Entities.Components;
using Xunit;
using Xunit.Abstractions;

namespace Alundra.Tests;

[Collection(AlundraMusicPlayerSingletonCollection.Name)]
public sealed class ZzAcquireProbeTests
{
    private readonly ITestOutputHelper _output;

    public ZzAcquireProbeTests(ITestOutputHelper output) => _output = output;

    [Fact]
    public void Probe_AcquireBeforeTheIntegration_ThenTheEnginesOwnAcquire_ReturnTheSameInstance()
    {
        using var arc = new ArcRun(new ArcSpec("E20e0a5-acquire", "Overworld", "Overworld 3,1-14", Array.Empty<int>(), 8, 6, 0, 2000, RealController: true, Prefabs: true));
        arc.OneFrame();
        var born = ((IEntityWorldContext)arc.Proxy).SpawnEntityBySpriteIndex(arc.Hero, false, 27, 100 << 16, 100 << 16, 0, 0x18)!;
        var entity = ArcRun.EntityOf(born)!;
        var sprite = entity.GetComponent<AnimatedSpriteComponent>()!;
        var game = arc.RealWorld!.Game;
        var before = sprite.AnimationAssetIds.Select(id => game.AssetContentManager.Acquire<Animation2dData>(id)).ToList();
        _output.WriteLine($"before the integration: world={(entity.World == null ? "null" : "set")} animations={sprite.Animations.Count} ids={sprite.AnimationAssetIds.Count}");
        arc.OneFrame();
        var after = sprite.AnimationAssetIds.Select(id => game.AssetContentManager.Acquire<Animation2dData>(id)).ToList();
        var same = before.Zip(after, (a, b) => ReferenceEquals(a.Asset, b.Asset)).All(x => x);
        var inSprite = before.All(h => sprite.Animations.Any(a => ReferenceEquals(a.Animation2dData, h.Asset)));
        _output.WriteLine($"after the integration: world={(entity.World == null ? "null" : "set")} animations={sprite.Animations.Count} same-instance={same} held-by-the-sprite={inSprite}");
        Assert.True(same && inSprite && before.Count > 0 && sprite.Animations.Count == before.Count);
    }
}
```

## The methods that create an entity through the interface, at the starting HEAD (T0, item 1)

HEAD `756ac59` (branch `chantier/e20-heros`), found by search of `SpawnEntityBySpriteIndex`, `CreateEntityFromSpriteIndex`, the seam `WeaponSpawnBirthHookForTests`, the set `WiredWeaponTypesForTests` and the helpers of the files that use them (`Shot`, `MountCarrier`,
`Spawn`, `Born`, `Fly`, `Walker`, `Arc`...), then checked against `dotnet test Alundra.Tests -c Release --no-build --list-tests` (4938 tests at the starting HEAD, 0 failures in Release): **80 methods, 111 test rows** in 13 classes. The list is
by reading and is an over-approximation by design (a method that only resets the seam is in it); the measure of the suite, not the list, decides what moves.

- `AlundraBirthTickProbePaTests` (5 methods, 5 rows)
  - `PA1_TheSpriteAskedForItsFirstAnimationBeforeTheIntegration_ThenIntegrated`
  - `PA1b_WithCurrentEqualTarget_PosedByHand_TheFirstFrameAfterTheIntegration`
  - `PA2_ControllerMoveBeforeTheIntegration_AddsTheDisplacementWithoutATest_AndAfterIt`
  - `PA3_TheGenericKinematicsOfTheBirthTick_WithCurrentAsTheInterfaceLeavesIt_MeasuredAfterE0a3b`
  - `PA3_TheGenericKinematicsOfTheBirthTick_WithCurrentEqualTarget_PosedByHand`
- `AlundraBirthTickTests` (10 methods, 17 rows)
  - `AnEntityFromARecord_KeysItsKinematicsOnItsCurrentAnimation_NeverOnTheTarget_EvenWithCurrentStillNotValidated`
  - `BT_H_BornInTheNativeProgramOfAnotherEntity_InATwoTickFrame_RunsAtTheSecondTick_NotInTheFirst`
  - `BT_H_BornInTheNativeProgramOfAnotherEntity_RunsFromTheNextTick_TheGapOfD_E20_34_OneTickFrames`
  - `BT_M_BornInMovePlayer_TakesPartInTheTickOfItsBirth_ProgramListsClockPhysics`
  - `BT_M_BornInTheFirstTickOfATwoTickFrame_ClockAndPhysicsRunTwice_AndANativeBirthIsNotRunTwice`
  - `DestroyedInTheTickOfItsBirth_InATwoTickFrame_IsRecycledAtTheHeadOfTheNextTick_ItsSlotIsGivenBack_AndTheEngineIntegratesNothingWrong`
  - `T0Prime_AnimFlagsAtBirth_ThenTheFirstFrameValidatesThePendingSwitch_InTheFourDirections` (8 rows)
  - `TheCreation_NeverReadsTheCollisionFieldOfTheHostByTheFallback_TheBirthTickDoes_AndAnEntityFromARecordNever`
  - `TheMarker_IsPosedByTheInterfaceOnly_AndNotCloned`
  - `TheSoundOfTheCreation_IsRequestedOnce_WhenTheEntityTakesPartInTheTickOfItsBirth`
- `AlundraEntitySlotV5RowsTests` (2 methods, 2 rows)
  - `V5_dirty_AFreeSlotWithLeftovers_IsTakenLowestFirst_AndTheCreationOverwritesWhatItWrites`
  - `V5_probes_TheThirteenInvalidIndicesOfTheOracle_ReturnNothing_WithOneMessage_AndTakeNothing`
- `AlundraEntitySlotWorldTests` (8 methods, 9 rows)
  - `ADestroyedEntityKeepsItsSlot_UntilTheRecyclingPassOpens_ThenTheSlotIsFree`
  - `AnApparitionTakesTheLowestFreeSlot_AndTheListFollowsTheOrderOfTheSlots`
  - `AnExceptionAfterTheAddition_LeavesNothingBehind_NullIsReturned_AndTheSlotIsFree` (2 rows)
  - `AnInvalidIndexWithAFullTable_IsRefusedForTheIndex_NotForTheTable_AsTheBinaryChecksThePageFirst`
  - `ChildEntity_ThroughBothPaths_RecordThenInterfaceThenRecord_KeepsTheSameRoot`
  - `TheEntriesWithoutASlot_StayWhereTheyAre_AndTheInsertionSkipsThem`
  - `TheSixtyFourthApparition_ByRecordId_IsRefused_WithALogLine_AndNothingAdded`
  - `TheSixtyFourthApparition_ThroughTheInterface_IsRefused_WithOneLogLine_AndNothingAdded`
- `AlundraLogicalZStepTests` (9 methods, 9 rows)
  - `A_TheMarker_IsPosedByTheInterfaceAndByItAlone_NotByTheHero_TheRecordCreations_TheRecycledEntities_NorTheClone`
  - `B1_TheBall_FollowsTheSeriesOfTheOracleMinusOneRawUnit_LandsAtTheTickOfTheOracle_AndTheSameBallWithoutGravityStaysAtItsHeight`
  - `B2_TheArrow_KeepsItsZ_IsOnGroundIsZeroWithTheRawUnitAndOneWithout_AndTheBallOnTheSameFloorLandsOnIt`
  - `B3_TheImpulseOf1024_FollowsTheSeriesOfTheCrateMinusOneRawUnit_AndLandsAtTheTickOfTheOracle_WhereTheSameEntityWithoutImpulseOnlyFalls`
  - `B4_TheExactGraze_DoesNotLandAtOnce_AndLandsAtTheNextTick_WhereAGrazeOneRawUnitDeeperLandsAtOnce`
  - `B5_TheCeiling_StopsTheRiseAtTheAbsoluteCeiling_WithTheContactAndTheForceKeptWithoutGravity_ClearedWithIt_AndALowerRiseGoesOn`
  - `B6_TheMagnet_SnapsAtMostThreePixelsAboveAndThreePixelsPlusOneUnitBelow_OnlyWithGravityForceZeroAndAnAcceptedXyStep`
  - `B7_TheStopMarker0x8000_ZeroesTheForceWithoutGravity_WhereWithGravityItIsTheImpulseShiftedByEight`
  - `B8_TheStep_DeclaresTheSentinelAndThePositionTheRootFollows_ThroughAVerticalMoveOnly_AndTheHeroIsLeftAlone`
- `AlundraLogicalZWorldTests` (11 methods, 18 rows)
  - `Z10_ATeleportInFlight_KeepsTheMarkedEntityInTheAirAgainstAnEngineUpdateWithoutATick_WhereTheRecordEntityIsSetBackOnTheGround`
  - `Z11_TheMarkedEntity_KeepsTheTerrainHeightFieldAtItsPosition_TheForceOfTheLandingTick_AndTheLogicalGroundFlagAcrossAnEngineUpdate`
  - `Z1_AnArrowBornAtTheHeightOfTheHero_KeepsHeroPlusOneRawUnitAndIsNotOnTheGroundFor30Ticks_WhereTheRecordEntityIsUnchanged` (3 rows)
  - `Z2_AnArrowOver16PxIsNotOnTheGroundAtAnyTick_AndAgainst32PxItIsStoppedAtTheTickOfTheOracle`
  - `Z3_TheBallAgainstAStepOf32Px_IsStoppedAtTheTickOfTheOracleAndFallsToTheGround_AndOnAStepOf16Px_ItIsPosedOnItAtTheTickOfTheOracle`
  - `Z3_TheBall_FollowsTheSeriesOfTheOracleFromTheFirstTick_AndLandsAtTheTickOfTheOracle` (3 rows)
  - `Z5_AFreezeAndAThawInTheAir_LetTheZGoOnOnTheSeriesOfTheOracle_AndNeverAnchorTheEntity_WhereTheRecordEntityIsUnchanged`
  - `Z6_TheRootFollowsTheLogicalZToTheRawUnit_AndAnEngineUpdateWithoutATickDoesNotMoveIt` (3 rows)
  - `Z7_TheStepHeight_IsZeroInTheAir_TheValueOfThePrefabAtRestWithGravity_AndZeroWithoutGravity`
  - `Z8_TheBallAndTheFlushArrow_HaveTheSameStateAtTicks6And12_Whatever1_2Or3TicksPerFrame_AndItIsTheOracles`
  - `Z9_TheMagnetOfTheBinary_PosesAWalkerOnTheLowerGroundWithin3Px_AndLeavesItInTheAirBeyond_OnTheSeriesOfTheOracle` (2 rows)
- `AlundraMapEdgeWorldTests` (3 methods, 3 rows)
  - `B1_AnArrowBornAtX1200_IsBoundedAtTheTickOfTheOracleWithTheFlagTheSameTick_AndAnArrowBornAtX1100IsNotBoundedFor20Ticks`
  - `B3_ARecordWalkerAtTheEdgeIsNotBounded_WhereTheMarkedArrowIsBounded`
  - `B4_TheHeroPushedEast_GoesPastItsOwnBoundAtTheTickItsSpeedGives_WhereTheMarkedArrowIsBounded`
- `AlundraMapStateTests` (2 methods, 4 rows)
  - `A_TheMapState_IsPosedOnTheEntityBornWithAMap_AndTheSameCreationWithoutAMapChangesNothing` (3 rows)
  - `A_TheTwoWordsAreTheOnesOfTheMapGiven_NotConstants`
- `AlundraMapStateWorldTests` (4 methods, 4 rows)
  - `K1_AnArrowBornWithTheMap_StopsAtTheWallOfTheMaskAtTheXOfTheOracle_AndTheSameArrowInAFreeCorridorFliesTheWholeDistance`
  - `K2_ABallBornWithTheMap_FallsAndLandsAtTheTickOfTheOracle_AndTheSameBallWithoutAMapStaysInTheAir`
  - `K3_OnMap159_TheBallBarelyFalls_WhereOnMap14_ItHasLandedAtTheSameTick`
  - `K4_TheFieldsOfTheEntityBornByTheInterface_AreTheOnesOfAnEntityOfTheSameSpriteCreatedFromARecord`
- `AlundraShotDirectionTests` (4 methods, 18 rows)
  - `D1_InTheAir_SquarePlusLeftAtTickP_ShotsAtPPlus1_ToTheWest_At1TickAnd2TicksPerFrame`
  - `D2_InTheAir_SquareAtTickP_LeftOnlyFromTheShotTick_ShotsToTheEast_TheTurnOfTheShotTickIsIgnored`
  - `D3_OnTheGround_SquarePlusADirection_ShotsInThatDirection` (4 rows)
  - `D4_TheBoxDirection_EqualsTheFacingOfTheHeadOfTheTick_AtTheTickOfTheShot` (12 rows)
- `AlundraSpawnBySpriteIndexCoreTests` (8 methods, 8 rows)
  - `B_TheCaseAndTheSeries_AreNotWrittenYet_E0a2`
  - `B_TheCreationImage_OfThe151PrefabsOfTheGlobalPage_IsTheOnesOfTheBinary`
  - `B_TheCreationImage_OfThe244NumbersOfTheMapPage_IsTheOnesOfTheBinary_OneImagePerNumber`
  - `B_TheGeometryVectors_ZParentDirectionAndTerrain_AreTheOnesOfTheBinary`
  - `B_ThePrefabsWithAnOffsetZ_AppearedAboveTheGround_KeepTheirModZ_TheMutantOfZMinusModZWouldSurvive`
  - `E_TheParentAndTheRoot_FourCases`
  - `F_TheEightCallsOfTheHeroShotJournalledByTheAt9Oracle_GiveTheBinarysImage_TheInterfaceAddsTheRawUnit`
  - `G_TheCreationSound_IsTheOneOfTheAnimation0_7PrefabsOfTheGlobalPageAnd9NumbersOfTheMapPage`
- `AlundraSpawnBySpriteIndexInterfaceTests` (10 methods, 10 rows)
  - `B2_TheFullPath_On151NumbersOfTheGlobalPage_AndTheNumbersOfTheMapPageValidOnMap14_GivesTheProjectionOfTheOracle_AndAppendsToTheWorld`
  - `C_TheNullContract_AnInvalidIndexAnAbsentPrefabOrAnUnknownMap_AddsNothing_AndLogsOneLine`
  - `D_TheNewEntity_IsNotFoundBy0x2COr0x2EOf0_AndItsTriggerIsMinusOne`
  - `E_TheParentAndTheRoot_ThroughTheInterface_FourCases`
  - `F_TheEightCallsOfTheAt9Traces_AppearAtTheZOfTheBinaryMinusOneRawUnit_UnlessTheTerrainRaisesThem`
  - `F_TheRawUnitOfZ_IsAddedByTheInterface_AHeroZMakesAnEntityOneRawUnitHigher`
  - `G_TheSoundOfTheAnimation0_IsRequestedOnceAtTheTickOfTheCall_AndOnlyWhereTheBinaryPlaysOne`
  - `H_TheNumber79OfTheMapPage_ByTheInterface_AndTheRecord0OfMap14_ByTheProductionPath_DifferOnlyByTheClosedList`
  - `L_TheIndicesOfTheMapPage_AreReadAtTheFirstCallOfTheMapPage_NeverAtTheLoadOfAWorld`
  - `NoOpContext_AndEveryExistingImplementer_KeepCompiling_TheDefaultOfTheInterfaceIsNull`
- `AlundraWeaponSpawnSeamTests` (4 methods, 4 rows)
  - `S1_TheEmptySet_CountsAndJournalsTheShotAndCreatesNothing_TheSetWithTheArrow_CreatesOneEntityInTheLowestFreeSlot`
  - `S2_WithTheHook_TheHooksEntityIsTheOnlyOne_WithoutTheHook_TheProductionCallMakesOne`
  - `S3_OnAMontageHost_TheShotIsCountedAndJournalled_NoInterfaceIsCalled_AndTheSameShotInTheWorldCreatesOne`
  - `S4_WithAFullTable_NothingIsCreatedAndThePointerIsCleared_WithOneFreeSlot_OneEntityIsCreated`

## The tests predicted to move (T0, item 2)

Phase A (the clock; no entity is born by the interface in production, only tests create one):
- certain: `AlundraBirthTickTests.BT_M_BornInTheFirstTickOfATwoTickFrame_ClockAndPhysicsRunTwice_AndANativeBirthIsNotRunTwice` (`ClockTicks` 1 becomes 2: the cursor of the second tick of the frame is the call n. 2, derived from `cnt1c`).
- possible, by mechanism: `AlundraBirthTickTests.BT_M_BornInMovePlayer_TakesPartInTheTickOfItsBirth_ProgramListsClockPhysics` (the assertion `ClockRan`, a mark of the uncounted switch tick, depends on whether the creation tick still loads the switch).
- no other test.

Phase B (the box; possible, by reading; remeasured at the start of T2 by the throwaway measure T2.0): the flights in the real world that launch an arrow or a ball, because the arrow gets a frame box and becomes an attacker of the hit pass for
the first time: `AlundraMapStateWorldTests` K1, K2, K3; `AlundraLogicalZWorldTests` Z1, Z2, Z3 (both), Z5, Z6, Z8, Z10, Z11 and the witness `Companion`; `AlundraWeaponSpawnSeamTests` S1, S2, S3, S4 and S6.

Stops of the slice that watch this list: a test outside the lists moves; more than 10 existing tests move; a pin of an entity born from a record or of the hero moves (the flag leaks).
