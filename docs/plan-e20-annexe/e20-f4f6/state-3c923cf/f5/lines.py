import subprocess, re, sys


def show(rev, path):
    try:
        return subprocess.run(['git', 'show', f'{rev}:{path}'], capture_output=True, check=True).stdout.decode('utf-8', 'replace').split('\n')
    except subprocess.CalledProcessError:
        return None


def work(path):
    try:
        return open(path, encoding='utf-8-sig').read().split('\n')
    except FileNotFoundError:
        return None


def find(lines, rx):
    if lines is None:
        return 'NO FILE'
    r = [i + 1 for i, l in enumerate(lines) if re.search(rx, l)]
    return r[0] if r else None


SC = 'Alundra/Scripts/'
items = [
    ('HitPass.AxisOverlaps', SC + 'AlundraHitPass.cs', r'internal static bool AxisOverlaps', 'Alundra/Combat/HitPass.cs', r'internal static bool AxisOverlaps'),
    ('HitPass for-loop over targets', SC + 'AlundraHitPass.cs', r'for \(var t = 0; t < count; t\+\+\)', 'Alundra/Combat/HitPass.cs', r'for \(var t = 0; t < count; t\+\+\)'),
    ('HitPass body Pos+Mod', SC + 'AlundraHitPass.cs', r'bodyX = target\.PosX \+ target\.ModX', 'Alundra/Combat/HitPass.cs', r'bodyX = target\.PosX \+ target\.ModX'),
    ('HitPass overlap W/H/D', SC + 'AlundraHitPass.cs', r'AxisOverlaps\(attacker\.HitBoxX, attacker\.CollisionWidth, bodyX, target\.Width\)', 'Alundra/Combat/HitPass.cs', r'AxisOverlaps\(attacker\.HitBoxX, attacker\.CollisionWidth, bodyX, target\.Width\)'),
    ('HitPass filter DamagedTickCounter', SC + 'AlundraHitPass.cs', r'target\.DamagedTickCounter != 0', 'Alundra/Combat/HitPass.cs', r'target\.DamagedTickCounter != 0'),
    ('HitPass InvulnerableAnimationBit', SC + 'AlundraHitPass.cs', r'InvulnerableAnimationBit =', 'Alundra/Combat/HitPass.cs', r'InvulnerableAnimationBit ='),
    ('HitPass ClassMaskOf', SC + 'AlundraHitPass.cs', r'static uint ClassMaskOf', 'Alundra/Combat/HitPass.cs', r'static uint ClassMaskOf'),
    ('HitCounter++', SC + 'AlundraHitPass.cs', r'attacker\.HitCounter\+\+', 'Alundra/Combat/HitPass.cs', r'attacker\.HitCounter\+\+'),
    ('StoreHitBox def', SC + 'AlundraEntityScriptProxy.HitPass.cs', r'internal void StoreHitBox', 'Alundra/Entities/EntityScriptProxy.HitPass.cs', r'internal void StoreHitBox'),
    ('StoreHitBox call (creation)', SC + 'AlundraEntityScriptProxy.HitPass.cs', r'StoreHitBox\(\); // E20.c1b K3', 'Alundra/Entities/EntityScriptProxy.HitPass.cs', r'StoreHitBox\(\); // E20.c1b K3'),
    ('StoreHitBox call (TickPhysics)', SC + 'AlundraEntityScriptProxy.cs', r'StoreHitBox\(\); // E20.c1b K3: 0x80038364', 'Alundra/Entities/EntityScriptProxy.cs', r'StoreHitBox\(\); // E20.c1b K3: 0x80038364'),
    ('HitBoxX field', SC + 'AlundraEntityScriptProxy.cs', r'public int HitBoxX;', 'Alundra/Entities/EntityScriptProxy.cs', r'public int HitBoxX;'),
    ('CollisionWidth field', SC + 'AlundraEntityScriptProxy.cs', r'public int CollisionWidth;', 'Alundra/Entities/EntityScriptProxy.cs', r'public int CollisionWidth;'),
    ('DamagedTickCounter field', SC + 'AlundraEntityScriptProxy.cs', r'public int DamagedTickCounter;', 'Alundra/Entities/EntityScriptProxy.cs', r'public int DamagedTickCounter;'),
    ('HitCounter field', SC + 'AlundraEntityScriptProxy.cs', r'public int HitCounter;', 'Alundra/Entities/EntityScriptProxy.cs', r'public int HitCounter;'),
    ('ForceResetAnimationFlag field', SC + 'AlundraEntityScriptProxy.cs', r'public int ForceResetAnimationFlag', 'Alundra/Entities/EntityScriptProxy.cs', r'public int ForceResetAnimationFlag'),
    ('HitAnimByte field', SC + 'AlundraEntityScriptProxy.HitPass.cs', r'public int HitAnimByte', 'Alundra/Entities/EntityScriptProxy.HitPass.cs', r'public int HitAnimByte'),
    ('opcode 0x86', SC + 'AlundraEventProgramRunner.cs', r'case 0x86:', 'Alundra/Events/EventProgramRunner.cs', r'case 0x86:'),
    ('DamagedTickCounter-- (counters pass)', SC + 'AlundraWorldProxy.cs', r'proxy\.DamagedTickCounter--', 'Alundra/Maps/WorldProxy.cs', r'proxy\.DamagedTickCounter--'),
    ('HitCounter = 0 (counters pass)', SC + 'AlundraWorldProxy.cs', r'proxy\.HitCounter = 0', 'Alundra/Maps/WorldProxy.cs', r'proxy\.HitCounter = 0'),
    ('ResyncControllerFromFlags def', SC + 'AlundraEntityScriptProxy.cs', r'internal void ResyncControllerFromFlags', 'Alundra/Entities/EntityScriptProxy.cs', r'internal void ResyncControllerFromFlags'),
    ('ApplyGravitySettingsToController def', SC + 'AlundraEntityScriptProxy.cs', r'internal void ApplyGravitySettingsToController', 'Alundra/Entities/EntityScriptProxy.cs', r'internal void ApplyGravitySettingsToController'),
    ('WalkabilityMaskFor', SC + 'AlundraCellsCollisionField.cs', r'public static uint WalkabilityMaskFor', 'Alundra/Physics/CellsCollisionField.cs', r'public static uint WalkabilityMaskFor'),
    ('TickScriptedNpc', SC + 'AlundraScriptedMotion.cs', r'internal static void TickScriptedNpc', 'Alundra/Physics/ScriptedMotion.cs', r'internal static void TickScriptedNpc'),
    ('speed-key line (JoinsBirthTick && ~0u)', SC + 'AlundraScriptedMotion.cs', r'RunOneMotionTick\(entity, entity\.JoinsBirthTick', 'Alundra/Physics/ScriptedMotion.cs', r'RunOneMotionTick\(entity, entity\.JoinsBirthTick'),
    ('TickPlayer key (TargetAnimationId)', SC + 'AlundraScriptedMotion.cs', r'RunOneMotionTick\(player, player\.TargetAnimationId\)', 'Alundra/Physics/ScriptedMotion.cs', r'RunOneMotionTick\(player, player\.TargetAnimationId\)'),
    ('RunOneKinematicTick', SC + 'AlundraScriptedMotion.cs', r'private static void RunOneKinematicTick', 'Alundra/Physics/ScriptedMotion.cs', r'private static void RunOneKinematicTick'),
    ('SyncAnimation def', SC + 'AlundraFrameSyncPasses.cs', r'internal static void SyncAnimation', 'Alundra/Maps/Loop/FrameSyncPasses.cs', r'internal static void SyncAnimation'),
    ('CurrentAnimationId assigned in SyncAnimation', SC + 'AlundraFrameSyncPasses.cs', r'proxy\.CurrentAnimationId = newCurrentAnimationId', 'Alundra/Maps/Loop/FrameSyncPasses.cs', r'proxy\.CurrentAnimationId = newCurrentAnimationId'),
    ('SpawnedEntities (interface)', SC + 'IEntityWorldContext.cs', r'SpawnedEntities \{ get; \}', 'Alundra/Maps/IEntityWorldContext.cs', r'SpawnedEntities \{ get; \}'),
    ('PlayerEntity (interface)', SC + 'IEntityWorldContext.cs', r'PlayerEntity \{ get; \}', 'Alundra/Maps/IEntityWorldContext.cs', r'PlayerEntity \{ get; \}'),
    ('SpawnedEntities impl', SC + 'AlundraWorldProxy.cs', r'IEntityWorldContext\.SpawnedEntities =>', 'Alundra/Maps/WorldProxy.cs', r'IEntityWorldContext\.SpawnedEntities =>'),
    ('hero InsertBySlot(entity, 0)', SC + 'AlundraWorldProxy.cs', r'InsertBySlot\(entity, 0\)', 'Alundra/Maps/WorldProxy.cs', r'InsertBySlot\(entity, 0\)'),
    ('GetSpawnedEntityProxies def', SC + 'AlundraWorldProxy.cs', r'private List<.*> GetSpawnedEntityProxies', 'Alundra/Maps/WorldProxy.cs', r'private List<.*> GetSpawnedEntityProxies'),
    ('DestroyEntity(e, effectId)', SC + 'AlundraWorldProxy.cs', r'internal void DestroyEntity\(.*int effectId\)', 'Alundra/Maps/WorldProxy.cs', r'internal void DestroyEntity\(.*int effectId\)'),
    ('RecycleDestroyedEntities def', SC + 'AlundraWorldProxy.cs', r'internal void RecycleDestroyedEntities', 'Alundra/Maps/WorldProxy.cs', r'internal void RecycleDestroyedEntities'),
    ('RunCountersPass', SC + 'AlundraWorldProxy.cs', r'private void RunCountersPass', 'Alundra/Maps/WorldProxy.cs', r'private void RunCountersPass'),
    ('PickEventTrigger', SC + 'AlundraEntityScriptProxy.cs', r'internal void PickEventTrigger', 'Alundra/Entities/EntityScriptProxy.cs', r'internal void PickEventTrigger'),
    ('E trigger flags test', SC + 'AlundraEntityScriptProxy.cs', r'DeactivateOnImpact\) != 0 && \(ForceAdjusted', 'Alundra/Entities/EntityScriptProxy.cs', r'DeactivateOnImpact\) != 0 && \(ForceAdjusted'),
    ('Deactivated -> program E', SC + 'AlundraEntityScriptProxy.cs', r'case EntityStatus\.Deactivated:', 'Alundra/Entities/EntityScriptProxy.cs', r'case EntityStatus\.Deactivated:'),
    ('RunSpriteEvent', SC + 'AlundraEventProgramRunner.cs', r'public void RunSpriteEvent', 'Alundra/Events/EventProgramRunner.cs', r'public void RunSpriteEvent'),
    ('E0/E1 in line', SC + 'AlundraEventProgramRunner.cs', r'IsInlineDeactivateCode\(entity\.SpriteProgramIndexes|IsInlineDeactivateCode\(|SpriteProgramIndexes\[ScriptHelper\.ProgramEDeactivate\] is 0', 'Alundra/Events/EventProgramRunner.cs', r'IsInlineDeactivateCode\(entity\.SpriteProgramIndexes'),
    ('NativeHandlersEnabled', SC + 'AlundraEventProgramRunner.cs', r'bool NativeHandlersEnabled', 'Alundra/Events/EventProgramRunner.cs', r'bool NativeHandlersEnabled'),
    ('NativeSprites property', SC + 'AlundraEventProgramRunner.cs', r'NativeSprites \{ get; \}', 'Alundra/Events/EventProgramRunner.cs', r'NativeSprites \{ get; \}'),
    ('Registry.CreateForProduction', SC + 'AlundraNativeSpriteRegistry.cs', r'CreateForProduction\(\)$', 'Alundra/Ai/NativeSpriteRegistry.cs', r'static NativeSpriteRegistry CreateForProduction'),
    ('Registry.Register', SC + 'AlundraNativeSpriteRegistry.cs', r'public IDisposable Register', 'Alundra/Ai/NativeSpriteRegistry.cs', r'public IDisposable Register'),
    ('Registry throw on duplicate', SC + 'AlundraNativeSpriteRegistry.cs', r'is already registered', 'Alundra/Ai/NativeSpriteRegistry.cs', r'is already registered'),
    ('Registry.Registration.Dispose', SC + 'AlundraNativeSpriteRegistry.cs', r'_owner\._entries\.Remove', 'Alundra/Ai/NativeSpriteRegistry.cs', r'_owner\._entries\.Remove'),
    ('ProductionWiredWeaponTypes', SC + 'AlundraPlayerManager.cs', r'IReadOnlySet<int> ProductionWiredWeaponTypes', 'Alundra/Hero/PlayerManager.cs', r'IReadOnlySet<int> ProductionWiredWeaponTypes'),
    ('WiredWeaponTypesForTests', SC + 'AlundraPlayerManager.cs', r'IReadOnlySet<int>\? WiredWeaponTypesForTests', 'Alundra/Hero/PlayerManager.cs', r'IReadOnlySet<int>\? WiredWeaponTypesForTests'),
    ('UnhandledWeaponSpriteCodes', SC + 'AlundraPlayerManager.cs', r'UnhandledWeaponSpriteCodes\(', 'Alundra/Hero/PlayerManager.cs', r'internal static .*UnhandledWeaponSpriteCodes\('),
    ('WeaponSpawnBirthHookForTests', SC + 'AlundraPlayerManager.cs', r'WeaponSpawnBirthHookForTests;', 'Alundra/Hero/PlayerManager.cs', r'WeaponSpawnBirthHookForTests;'),
    ('SpawnDirections', SC + 'AlundraPlayerManager.cs', r'int\[\] SpawnDirections', 'Alundra/Hero/PlayerManager.cs', r'int\[\] SpawnDirections'),
    ('HasLogicalZ = true', SC + 'AlundraWorldProxy.cs', r'HasLogicalZ = true', 'Alundra/Maps/WorldProxy.cs', r'HasLogicalZ = true'),
    ('JoinsBirthTick = true (interface)', SC + 'AlundraWorldProxy.cs', r'spawnedProxy\.JoinsBirthTick = true', 'Alundra/Maps/WorldProxy.cs', r'spawnedProxy\.JoinsBirthTick = true'),
    ('WeaponTables record 8', SC + 'AlundraWeaponTables.cs', r'0x00, 0x13, 0x03, 0x08, 0x10', 'Alundra/Items/WeaponTables.cs', r'0x00, 0x13, 0x03, 0x08, 0x10'),
    ('WeaponTables record 9', SC + 'AlundraWeaponTables.cs', r'0x01, 0x13, 0x04, 0x08, 0x10', 'Alundra/Items/WeaponTables.cs', r'0x01, 0x13, 0x04, 0x08, 0x10'),
    ('EntityFlags HitsClassA', SC + 'EntityFlags.cs', r'HitsClassA =', 'Alundra/Entities/EntityFlags.cs', r'HitsClassA ='),
    ('EntityStatus.FlagToDestroy', SC + 'EntityStatus.cs', r'FlagToDestroy = 4', 'Alundra/Entities/EntityStatus.cs', r'FlagToDestroy = 4'),
    ('Test registry pin (Count == 6/6+1)', SC.replace('Alundra/Scripts/', 'Alundra.Tests/') + 'AlundraNativeSpriteRegistryTests.cs', r'Assert\.Equal\(6', 'Alundra.Tests/AlundraNativeSpriteRegistryTests.cs', r'Assert\.Equal\(6'),
    ('Test registry pin method', 'Alundra.Tests/AlundraNativeSpriteRegistryTests.cs', r'public void ProductionRunner_RegistryHolds', 'Alundra.Tests/AlundraNativeSpriteRegistryTests.cs', r'public void ProductionRunner_RegistryHolds'),
    ('Test S5', 'Alundra.Tests/AlundraWeaponSpawnSeamTests.cs', r'public void S5_', 'Alundra.Tests/AlundraWeaponSpawnSeamTests.cs', r'public void S5_'),
    ('Test S1', 'Alundra.Tests/AlundraWeaponSpawnSeamTests.cs', r'public void S1_', 'Alundra.Tests/AlundraWeaponSpawnSeamTests.cs', r'public void S1_'),
    ('Test P3', 'Alundra.Tests/AlundraMapStateTests.cs', r'public void P3_', 'Alundra.Tests/AlundraMapStateTests.cs', r'public void P3_'),
    ('Test (p5) f3c', 'Alundra.Tests/AlundraWaveSwordWiringTests.cs', r'public void P5_R_', 'Alundra.Tests/AlundraWaveSwordWiringTests.cs', r'public void P5_R_'),
    ('Test B6_R', 'Alundra.Tests/AlundraRecordBirthTickTests.cs', r'public void B6_R_', 'Alundra.Tests/AlundraRecordBirthTickTests.cs', r'public void B6_R_'),
    ('Test V3 (seven keys)', 'Alundra.Tests/AlundraWaveSwordLifeTests.cs', r'public void V3_', 'Alundra.Tests/AlundraWaveSwordLifeTests.cs', r'public void V3_'),
    ('HitArena NativeHandlersEnabled=false', 'Alundra.Tests/AlundraHitArena.cs', r'NativeHandlersEnabled = false', 'Alundra.Tests/AlundraHitArena.cs', r'NativeHandlersEnabled = false'),
]

rev = sys.argv[1] if len(sys.argv) > 1 else '9ecc7d5'
print('| label | old (%s) | HEAD |' % rev)
print('|---|---|---|')
for label, of, orx, nf, nrx in items:
    ol = find(show(rev, of), orx)
    nl = find(work(nf), nrx)
    print(f'| {label} | {of}:{ol} | {nf}:{nl} |')
