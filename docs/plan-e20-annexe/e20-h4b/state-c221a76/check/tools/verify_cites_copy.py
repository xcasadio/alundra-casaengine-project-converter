"""verify_cites.py - read-only check of the file:line citations of state.md against the repository.
Each entry: (relative path, line, regex that must match somewhere on that line). Prints the failures only, then a count.
Run: python -B verify_cites.py   (from this folder; reads the repository, writes nothing)."""
import re
import sys

ROOT = r"D:/development/repo/alundra-casaengine-project-converter/"
A = "Alundra/"
T = "Alundra.Tests/"
SM = A + "Physics/ScriptedMotion.cs"
EP = A + "Entities/EntityScriptProxy.cs"
HP = A + "Entities/EntityScriptProxy.HitPass.cs"
WP = A + "Maps/WorldProxy.cs"
CS = A + "Cameras/CameraSway.cs"
CD = A + "Cameras/CameraDirector.cs"
CM = A + "Cameras/CameraMath.cs"
NR = A + "Ai/NativeSpriteRegistry.cs"
ST = A + "Entities/EntitySlotTable.cs"
IW = A + "Maps/IEntityWorldContext.cs"

C = [
    (SM, 49, r"No delegate/closure"), (SM, 88, r"static void TickPlayer"), (SM, 171, r"static void TickScriptedNpc"),
    (SM, 175, r"RunOneMotionTick\(entity, entity\.JoinsBirthTick"), (SM, 212, r"var logicalZ = entity\.HasLogicalZ"),
    (SM, 213, r"RunOneKinematicTick\(entity"), (SM, 216, r"RunLogicalZPostXyStep\(entity\)"),
    (SM, 465, r"bool RunLogicalZPreXyStep"), (SM, 540, r"void RunLogicalZPostXyStep"), (SM, 609, r"riding-platform force feed"),
    (SM, 614, r"void RunOneKinematicTick"), (SM, 625, r"entity\.Speed != speed"), (SM, 641, r"E19\.d2c1 R7"),
    (SM, 665, r"entity\.ForceX = IncrementForce"), (SM, 666, r"entity\.ForceY = IncrementForce"),
    (SM, 670, r"entity\.AdjustedForceX = entity\.ForceX"), (SM, 671, r"entity\.AdjustedForceY = entity\.ForceY"),
    (SM, 672, r"entity\.FinalForceX = entity\.AdjustedForceX"), (SM, 673, r"entity\.FinalForceY = entity\.AdjustedForceY"),
    (SM, 674, r"entity\.FinalForceZ = entity\.ForceZ"), (SM, 677, r"entity\.RidingEntity\?\.GameplayProxy is EntityScriptProxy carrier"),
    (SM, 685, r"if \(entity\.HasLogicalZ\)"), (SM, 687, r"ClampStepToMapEdge\(entity"),
    (SM, 700, r"if \(entity\.Controller != null\)"), (SM, 702, r"MoveControllerAndPullPosition"), (SM, 707, r"entity\.PosX \+= entity\.FinalForceX"),
    (SM, 712, r"if \(edgeAdjusted\)"), (SM, 717, r"entity\.LastTickDeltaX"), (SM, 741, r"MapEdgeClamp ClampStepToMapEdge"),
    (SM, 781, r"void SlideAlongWall"), (SM, 786, r"direction >= 32"), (SM, 810, r"if \(!agrees\)"), (SM, 819, r"NoObstacleSlide"),
    (SM, 632, r"AnimationTables\.OffsetXList"), (SM, 633, r"AnimationTables\.OffsetYList"),
    (EP, 82, r"internal int Slot = -1"), (EP, 88, r"public EntityStatus Status"), (EP, 99, r"public Entity\? BlockedByEntity"),
    (EP, 101, r"public Entity\? PlatformEntity"), (EP, 156, r"public uint Flags"), (EP, 337, r"public int AnimFlags"),
    (EP, 341, r"PreviousAdjustedForceX"), (EP, 342, r"PreviousAdjustedForceY"), (EP, 351, r"public int PosX"), (EP, 352, r"public int PosY"),
    (EP, 483, r"DamagedTickCounter"), (EP, 484, r"FrameCollisionTickCounter"),
    (EP, 1227, r"public byte\[\] Bytes"), (EP, 1228, r"DelayOrAngleOrEntityId"), (EP, 1229, r"ItemState"), (EP, 1230, r"public short\[\] AIValues"),
    (EP, 1375, r"internal bool JoinsBirthTick"), (EP, 1383, r"internal bool HasLogicalZ"),
    (EP, 1527, r"TickPhysics\(\)"), (EP, 1546, r"TickPhysics\(\)"), (EP, 1602, r"internal void TickPhysics"), (EP, 1614, r"private void TickMotion"),
    (EP, 1616, r"if \(IsPlayer\)"), (EP, 1620, r"PlayerManager\.Tick\(this, 1\)"), (EP, 1629, r"Status\.IsActive\(\)"), (EP, 1631, r"ScriptedMotion\.TickScriptedNpc"),
    (EP, 1718, r"DeactivateOnImpact"), (EP, 2435, r"ForceAdjusted = 1"), (EP, 2638, r"PreviousAdjustedForceX = "), (EP, 2639, r"PreviousAdjustedForceY = "),
    (EP, 2716, r"Bytes = \(byte\[\]\)Bytes\.Clone"), (EP, 2717, r"DelayOrAngleOrEntityId = "), (EP, 2718, r"ItemState = "), (EP, 2719, r"AIValues = \(short\[\]\)AIValues\.Clone"),
    (HP, 24, r"public BalanceRecord\? BalanceRecord"), (HP, 30, r"public int HitAnimByte"),
    (A + "Entities/EntityStatusExtensions.cs", 11, r"IsActive"), (A + "Entities/EntityStatusExtensions.cs", 12, r"Normal or EntityStatus\.Deactivated"),
    (WP, 170, r"EntitySlotTable _slots"), (WP, 176, r"SlotTableFactoryForTests"), (WP, 179, r"SlotTable =>"), (WP, 182, r"private int HighestLiveSlot"),
    (WP, 200, r"private int HoldSlot"), (WP, 216, r"private void InsertBySlot"), (WP, 257, r"private void ReportSlot"), (WP, 264, r"HighestLiveSlot\(\)"),
    (WP, 699, r"CameraSway\.Instance"), (WP, 701, r"CameraSway CameraSway"), (WP, 821, r"DrivesEntityTicks =>"), (WP, 880, r"CameraSway\.Instance\.OnMapLoad"),
    (WP, 1074, r"_slots\.Allocate"), (WP, 2302, r"proxy\.Slot = 0"), (WP, 2304, r"InsertBySlot\(entity, 0\)"), (WP, 2524, r"for \(var tick = 0; tick < ticksThisFrame"),
    (WP, 2557, r"PlayerEntity != null && !GameplayGateNow"), (WP, 2572, r"RecycleDestroyedEntities\(\)"),
    (WP, 2716, r"UpdateCameraFollow"), (WP, 2865, r"private void CollectTickEntities"), (WP, 2907, r"if \(gateClosed\)"), (WP, 2976, r"TickTraceForTests\?\.Invoke\(\"physics\"\)"),
    (WP, 2979, r"TickPhysics\(\)"), (WP, 3265, r"void RecycleDestroyedEntities"), (WP, 3267, r"GameplayBlockedMask"), (WP, 3284, r"_slots\.Release"),
    (WP, 3399, r"_slots\.Allocate"), (WP, 3528, r"_slots\.Allocate"), (WP, 3572, r"JoinsBirthTick = true"), (WP, 3573, r"HasLogicalZ = true"),
    (WP, 3657, r"List<EntityScriptProxy> GetSpawnedEntityProxies"),
    (CS, 26, r"static readonly CameraSway Instance"), (CS, 33, r"int Flag"), (CS, 41, r"int ReachY"), (CS, 45, r"void Start"), (CS, 55, r"void Stop"),
    (CS, 58, r"void OnMapLoad"), (CS, 65, r"void Step"), (CS, 116, r"void ResetForTests"),
    (CD, 214, r"void UpdateCameraFollow"), (CD, 222, r"IsTransitionInProgress"), (CD, 224, r"_debugCamera == null"), (CD, 262, r"snapsThisFrame"),
    (CD, 268, r"AdvanceCameraSmoothing"), (CD, 270, r"swayActive \? sway : null"), (CD, 272, r"ticksThisFrame > 0"),
    (CM, 167, r"Vector3 AdvanceCameraSmoothing"), (CM, 174, r"if \(needsSnap\)"), (CM, 185, r"for \(var tick = 0; tick < fullTicks"),
    (CM, 216, r"Vector3 ComputeSmoothedCameraTarget"), (CM, 223, r"-swayOffsetY"), (CM, 225, r"mapWidthPx\.HasValue"), (CM, 301, r"ToOriginalScrollSpace"),
    (NR, 20, r"delegate void NativeSpriteHandler"), (NR, 36, r"CreateForProduction"), (NR, 54, r"IDisposable Register"), (NR, 60, r"_entries\.ContainsKey"),
    (NR, 62, r"InvalidOperationException"), (NR, 70, r"bool TryGet"), (NR, 95, r"public void Dispose"), (NR, 97, r"_owner\._entries\.Remove"),
    (A + "Events/EventProgramRunner.cs", 283, r"E19\.d2b"), (A + "Events/EventProgramRunner.cs", 328, r"IsInlineDeactivateCode"),
    (A + "Events/EventProgramRunner.cs", 791, r"GetMatchingEntitiesBySearchType"), (A + "Events/EventProgramRunner.cs", 3382, r"GetMatchingEntitiesBySearchType"),
    (A + "Entities/EntitySearchService.cs", 75, r"public static class EntitySearchService"), (A + "Entities/EntitySearchService.cs", 99, r"GetMatchingEntitiesBySearchType"),
    (ST, 17, r"SlotCount = 64"), (ST, 19, r"_held = 1UL"), (ST, 22, r"HeldCount"), (ST, 25, r"IsHeld"), (ST, 28, r"int Allocate"), (ST, 44, r"HoldHeroSlot"), (ST, 47, r"void Release"),
    (A + "Entities/AnimationTables.cs", 71, r"OffsetXList"), (A + "Entities/AnimationTables.cs", 82, r"OffsetYList"),
    (A + "Ai/Objects/SaveBook.cs", 161, r"one little-endian int"), (A + "Ai/Objects/SaveBook.cs", 164, r"book\.Bytes\[0\]"), (A + "Ai/Objects/SaveBook.cs", 166, r"WriteState"),
    (A + "Ai/Objects/SaveBook.cs", 264, r"DelayOrAngleOrEntityId"), (A + "Ai/Objects/SaveBook.cs", 333, r"DelayOrAngleOrEntityId"),
    (A + "Ai/Enemies/MurutaAi.cs", 56, r"AIValues\[0\]"), (A + "Ai/Enemies/MurutaAi.cs", 93, r"Bytes\[3\]"), (A + "Ai/Enemies/MurutaAi.cs", 150, r"AIValues\[1\]"),
    (A + "Ai/Enemies/MurutaAi.cs", 215, r"AIValues\[1\]"), (A + "Ai/Enemies/MurutaAi.cs", 330, r"AIValues\[1\]"), (A + "Ai/Enemies/MurutaAi.cs", 354, r"Bytes\[1\]"),
    (A + "Ai/Enemies/MurutaAi.cs", 361, r"AIValues\[1\]"),
    (A + "Ai/Enemies/BeannoideAi.cs", 74, r"AIValues\[1\]"), (A + "Ai/Enemies/BeannoideAi.cs", 89, r"Bytes\[1\]"), (A + "Ai/Enemies/BeannoideAi.cs", 161, r"Bytes\[3\]"),
    (A + "Ai/Enemies/BeannoideAi.cs", 196, r"AIValues\[1\]"), (A + "Ai/EnemyHelpers.cs", 221, r"AIValues\[1\]"), (A + "Ai/EnemyHelpers.cs", 234, r"Bytes\[3\]"),
    (A + "Hero/PlayerManager.cs", 1218, r"world\.SpawnEntityBySpriteIndex"), (A + "Hero/PlayerManager.cs", 1839, r"public static void Tick"),
    (A + "Hero/PlayerManager.cs", 1840, r"ScriptedMotion\.TickPlayer"),
    (IW, 26, r"public interface IEntityWorldContext"), (IW, 81, r"SpawnEntityBySpriteIndex"), (IW, 116, r"ICellMutator\? CellMutator"), (IW, 193, r"CameraSway\? CameraSway"),
    (A + "Combat/BalanceRecord.cs", 76, r"public int ByteAt"), (A + "Combat/BalanceRecord.cs", 78, r"Following != null"),
    (A + "Entities/Spawning/EntitySpawnFactory.cs", 541, r"isMapPage \? index \| 0x100 : index"),
    (A + "Probes/SlotShadow.cs", 15, r"EnvVar"),
    ("alundra-datas-analyser/AlundraTools/AlundraEngine/Gameplay/Scripts/ScriptHelper.cs", 126, r"GetInt32\(this byte\[\]"),
    ("alundra-datas-analyser/AlundraTools/AlundraEngine/Gameplay/Scripts/ScriptHelper.cs", 166, r"GetInt32\(this short\[\]"),
    ("alundra-datas-analyser/AlundraTools/AlundraEngine/Gameplay/Scripts/ScriptHelper.cs", 171, r"Set\(this short\[\] array, int"),
    ("alundra-datas-analyser/AlundraTools/AlundraEngine/Gameplay/Scripts/FunctionTypeC.cs", 16886, r"g_cosinus"),
    ("alundra-datas-analyser/AlundraTools/AlundraEngine/PhysicsEngine.cs", 1518, r"PreviousAdjustedForceX"),
    # tests
    (T + "AlundraBeannoideOracleTests.cs", 73, r"b0_274"), (T + "AlundraBeannoideOracleTests.cs", 77, r"ai1_282"), (T + "AlundraBeannoideOracleTests.cs", 90, r"UntouchedWords"),
    (T + "AlundraBeannoideOracleTests.cs", 93, r"prevadjx_CC"), (T + "AlundraBeannoideOracleTests.cs", 94, r"prevadjy_D0"), (T + "AlundraBeannoideOracleTests.cs", 178, r"UntouchedWords"),
    (T + "AlundraBeannoideOracleTests.cs", 230, r"UntouchedWords"), (T + "AlundraBeannoideOracleTests.cs", 263, r"NativeHandlerRunCount"), (T + "AlundraBeannoideOracleTests.cs", 330, r"NativeHandlerRunCount"),
    (T + "AlundraMurutaOracleTests.cs", 72, r"b0_274"), (T + "AlundraMurutaOracleTests.cs", 76, r"ai1_282"), (T + "AlundraMurutaOracleTests.cs", 89, r"UntouchedWords"),
    (T + "AlundraMurutaOracleTests.cs", 92, r"prevadjx_CC"), (T + "AlundraMurutaOracleTests.cs", 93, r"prevadjy_D0"), (T + "AlundraMurutaOracleTests.cs", 210, r"UntouchedWords"),
    (T + "AlundraMurutaOracleTests.cs", 261, r"UntouchedWords"), (T + "AlundraMurutaOracleTests.cs", 316, r"NativeHandlerRunCount"), (T + "AlundraMurutaOracleTests.cs", 397, r"Bytes\[3\]"),
    (T + "AlundraMurutaOracleTests.cs", 398, r"NativeHandlerRunCount"), (T + "AlundraMurutaOracleTests.cs", 412, r"AIValues\[0\]"),
    (T + "AlundraEntityScriptProxyCloneTests.cs", 41, r"Bytes\[0\]"), (T + "AlundraEntityScriptProxyCloneTests.cs", 42, r"AIValues\[4\]"), (T + "AlundraEntityScriptProxyCloneTests.cs", 55, r"Bytes"),
    (T + "AlundraEntityScriptProxyCloneTests.cs", 59, r"NotSame\(original\.AIValues"),
    (T + "AlundraNativeSpriteRegistryTests.cs", 14, r"E14\.a0 T2"), (T + "AlundraNativeSpriteRegistryTests.cs", 51, r"SevenHandlers"), (T + "AlundraNativeSpriteRegistryTests.cs", 55, r"6 \+ 1"),
    (T + "AlundraNativeSpriteRegistryTests.cs", 85, r"NoRegistrationListsTheTypeMinus1"), (T + "AlundraNativeSpriteRegistryTests.cs", 207, r"SaveBook_DoesNotGoThroughTheRegistry"),
    (T + "AlundraNativeSpriteRegistryTests.cs", 239, r"Registering_TheSameKeyTwice_Throws"),
    (T + "AlundraWaveSwordLifeTests.cs", 167, r"SevenKeys"), (T + "AlundraWaveSwordLifeTests.cs", 182, r"3 \+ 3 \+ 1"), (T + "AlundraWaveSwordLifeTests.cs", 183, r"registry\.Count"),
    (T + "AlundraWeaponSpawnSeamTests.cs", 238, r"S5_"), (T + "AlundraWeaponSpawnSeamTests.cs", 251, r"ProgramEDeactivate, 17"),
    (T + "AlundraMapEdgeWorldTests.cs", 120, r"B1_"), (T + "AlundraMapEdgeWorldTests.cs", 183, r"B2_"), (T + "AlundraMapEdgeWorldTests.cs", 196, r"B2_"),
    (T + "AlundraMapEdgeWorldTests.cs", 213, r"B3_"), (T + "AlundraMapEdgeWorldTests.cs", 275, r"B4_"),
    (T + "AlundraMapEdgeClampTests.cs", 46, r"A_AStep"), (T + "AlundraMapEdgeClampTests.cs", 62, r"B_TheFourEdges"), (T + "AlundraMapEdgeClampTests.cs", 99, r"C_AStepThat"),
    (T + "AlundraLogicalZWorldTests.cs", 133, r"Z1_"), (T + "AlundraLogicalZWorldTests.cs", 545, r"Z11_"),
    (T + "AlundraLogicalZStepTests.cs", 306, r"FinalForceX"), (T + "AlundraLogicalZStepTests.cs", 335, r"FinalForceX"), (T + "AlundraLogicalZStepTests.cs", 355, r"FinalForceZ"),
    (T + "AlundraBirthTickTests.cs", 458, r"TickScriptedNpc"), (T + "AlundraBirthTickTests.cs", 485, r"proxy\.AnimFlags"), (T + "AlundraBirthTickTests.cs", 496, r"proxy\.HitAnimByte"),
    (T + "AlundraBirthTickProbePaTests.cs", 202, r"TickScriptedNpc"),
    (T + "AlundraRecordBirthTickTests.cs", 396, r"OffsetXList"),
    (T + "AlundraCameraSwayFollowTests.cs", 41, r"AdvanceCameraSmoothing"), (T + "AlundraCameraSwayFollowTests.cs", 69, r"ASnapFrameOfThreeTicks"), (T + "AlundraCameraSwayFollowTests.cs", 82, r"ASnapFrameOfZeroTicks"),
    (T + "AlundraCameraSwayFollowTests.cs", 132, r"AdvanceCameraSmoothing"),
    (T + "AlundraWorldProxyCameraFollowTests.cs", 246, r"ComputeSmoothedCameraTarget"), (T + "AlundraWorldProxyCameraFollowTests.cs", 278, r"AdvanceCameraSmoothing"),
    (T + "AlundraWorldProxyCameraFollowTests.cs", 466, r"AdvanceCameraSmoothing"), (T + "AlundraWorldProxyCameraFollowTests.cs", 477, r"AdvanceCameraSmoothing"),
    (T + "AlundraCameraSwayTests.cs", 28, r"ResetForTests"), (T + "AlundraCameraSwayTests.cs", 37, r"ResetForTests"), (T + "AlundraCameraSwayTests.cs", 220, r"InitializeWithWorld_ClearsOnlyTheFlag"),
    (T + "AlundraCameraSwayTests.cs", 239, r"ResetForTests_ClearsAllNineFields"), (T + "AlundraCameraSwayWiringTests.cs", 39, r"ResetForTests"),
    (T + "SaveGameDirectorTestSupport.cs", 134, r"CameraSway\.Instance\.ResetForTests"),
    (T + "AlundraEntitySlotWorldTests.cs", 77, r"TheHeroHoldsSlot0"), (T + "AlundraEntitySlotWorldTests.cs", 97, r"AnApparitionTakesTheLowestFreeSlot"),
    (T + "AlundraEntitySlotWorldTests.cs", 117, r"TheEntriesWithoutASlot"), (T + "AlundraEntitySlotWorldTests.cs", 139, r"ADestroyedEntityKeepsItsSlot"),
    (T + "AlundraEntitySlotWorldTests.cs", 169, r"TheSixtyFourthApparition_ThroughTheInterface"),
    (T + "AlundraEntitySlotTableTests.cs", 26, r"V5_five"), (T + "AlundraEntitySlotTableTests.cs", 97, r"TheHeroSlot0"), (T + "AlundraEntitySlotTableTests.cs", 161, r"TwoTables_AreIndependent"),
    (T + "AlundraHeroRunTablesTests.cs", 46, r"DashDirectionByNibble"), (T + "AlundraHeroRunTablesTests.cs", 64, r"SprintConeRows"),
    (T + "AlundraEnemyHitReactionTests.cs", 58, r"Bytes\[3\]"), (T + "AlundraEnemyHitReactionTests.cs", 80, r"Bytes\[3\]"), (T + "AlundraEnemyHitReactionTests.cs", 96, r"Bytes\[3\]"),
    (T + "AlundraEnemyHelpersTests.cs", 145, r"AIValues\[1\]"), (T + "AlundraEnemyHelpersTests.cs", 151, r"AIValues\[1\]"),
    (T + "AlundraSaveBookDispatchTests.cs", 146, r"entity\.Bytes"), (T + "AlundraHitArena.cs", 70, r"NativeHandlersEnabled|Native"),
    (T + "AlundraBirthBoxTests.cs", 131, r"."), (T + "AlundraMurutaHallArcTests.cs", 100, r"NativeHandlerRunCounts"),
    (T + "AlundraDestroyedEntityRecyclingTests.cs", 63, r"GetMatchingEntitiesBySearchType"),
]

fails = 0
cache = {}
for path, line, rx in C:
    full = ROOT + path
    if full not in cache:
        try:
            cache[full] = open(full, encoding="utf-8-sig").read().split("\n")
        except Exception as ex:
            cache[full] = None
            print("UNREADABLE", path, ex)
    lines = cache[full]
    if lines is None:
        fails += 1
        continue
    if line > len(lines) or not re.search(rx, lines[line - 1]):
        fails += 1
        print("FAIL %s:%d  expected /%s/  got: %s" % (path, line, rx, (lines[line - 1].strip()[:110] if line <= len(lines) else "<past end>")))
print("%d citations checked, %d failures" % (len(C), fails))
sys.exit(1 if fails else 0)
