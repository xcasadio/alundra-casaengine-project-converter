import subprocess, sys, os, re, time

REPO = 'D:/development/repo/alundra-casaengine-project-converter'
S = 'C:/Users/casad/AppData/Local/Temp/claude/D--development-repo-alundra-casaengine-project-converter/b00d1a72-420d-4acc-ab34-0c25fbdad3a1/scratchpad'
K = '-p:CustomAfterMicrosoftCommonTargets=' + S + '/e20e0a3b-exec/skipcopy.targets'
CLASSES = ['CarryGroupTests', 'CarryGuardTests', 'CarryLockSwimAndStatesTests', 'CarryRulesModesTests', 'CarryRulesStepTests',
           'CarryTopsAndPlatformTests', 'CarryWalkAndJumpTests', 'CarryWorldTests', 'PutInHandsTests', 'LiftableAiMap392Tests',
           'LiftableAiRegistrationTests', 'LiftableAiTests', 'DestroyEntityCarrierTests', 'CarriedSortKeyTests', 'NativeSpriteRegistryTests',
           'EntityDestructionTests', 'CarryFollowAndGestureTests']
FILT = '|'.join('FullyQualifiedName~.' + c for c in CLASSES)
OUT = S + '/g1a-t5/mut5-results.txt'

CR = 'Alundra/Hero/CarryRules.cs'
LA = 'Alundra/Ai/Objects/LiftableAi.cs'
PM = 'Alundra/Hero/PlayerManager.cs'
ESP = 'Alundra/Entities/EntityScriptProxy.cs'
WP = 'Alundra/Maps/WorldProxy.cs'
FS = 'Alundra/Maps/Loop/FrameSyncPasses.cs'
ES = 'Alundra/Physics/EntitySupport.cs'
ED = 'Alundra/Entities/EntityDepthKey.cs'
NR = 'Alundra/Ai/NativeSpriteRegistry.cs'

FORCES = "        ForceX = 0;\n        ForceY = 0;\n        ForceZ = 0;\n        TargetForceX = 0;\n        TargetForceY = 0;\n        FinalForceX = 0;\n        FinalForceY = 0;\n        FinalForceZ = 0;\n        ForceAdjusted = 1;"

MUTS = [
    ('M01', 'mode 0: "<" becomes "<=" at the equality', CR, 'horizontal < error', 'horizontal <= error'),
    ('M02', 'free step 1 px becomes 2 px', CR, 'internal const int FreeStep = 0x10000;', 'internal const int FreeStep = 0x20000;'),
    ('M03', 'destroy threshold 0x1FFFF becomes 0x20000', CR, 'internal const int DestroyLimit = 0x1FFFF;', 'internal const int DestroyLimit = 0x20000;'),
    ('M04', 'the 5 of the counter becomes 4', CR, 'internal const int LockedTicksBeforePull = 5;', 'internal const int LockedTicksBeforePull = 4;'),
    ('M05', 'counter not reset in mode 0', CR, "WriteOffset(hero, FreeOffset(Difference(hero, item)));\n                hero.CarryLockCounter = 0;", "WriteOffset(hero, FreeOffset(Difference(hero, item)));"),
    ('M06', 'mode 1 -> mode 0 forgotten for AttackCount 0', CR, 'if (mode == 1 && (item.BalanceRecord?.Attacks.Count ?? 0) == 0)', 'if (false)'),
    ('M07', 'destruction of mode 1 with two arguments (break effect)', WP, "entity.Status = EntityStatus.FlagToDestroy;\n        entity.EventTrigger = ScriptHelper.ProgramUnknown;\n        ClearCarrierOf(entity);", "DestroyEntity(entity, -1);\n        entity.EventTrigger = ScriptHelper.ProgramUnknown;"),
    ('M07b', 'mode 1 destroys through the two-argument overload of the world proxy', CR, 'world?.DestroyEntity(item);', '(world as WorldProxy)?.DestroyEntity(item, -1);'),
    ('M08', 'offset read from the offset words instead of the positions', CR, 'new(unchecked(item.PosX - hero.PosX), unchecked(item.PosY - hero.PosY), unchecked(item.PosZ - hero.PosZ))', 'new(hero.RelativeWarpOffsetX, hero.RelativeWarpOffsetY, hero.RelativeWarpOffsetZ)'),
    ('M09', 'mode 2 without the -1', CR, 'item.Flags2 = -1;', 'item.Flags2 = 0;'),
    ('M10', 'mode 2 clears CarriedEntity', CR, 'item.Flags2 = -1;', 'item.Flags2 = -1; hero.CarriedEntity = null;'),
    ('M11', 'follow without the copy of the heights', ESP, "        FloorHeight = carrier.FloorHeight;\n        TerrainHeight = carrier.TerrainHeight;\n", ""),
    ('M12', 'follow keeps the forces', ESP, FORCES, "        ForceAdjusted = 1;"),
    ('M13', 'follow does not clear XCollisionEntity', ESP, "        XCollisionEntity = null;\n        IsOnGround = 0;\n        FloorHeight = carrier.FloorHeight;", "        IsOnGround = 0;\n        FloorHeight = carrier.FloorHeight;"),
    ('M14', 'carried object left in Collidables', ES, "            && (entity.AnimFlags & NoEntityCollisionAnimFlag) == 0\n            && entity.PlatformEntity == null;", "            && (entity.AnimFlags & NoEntityCollisionAnimFlag) == 0;"),
    ('M15', 'clearing of the carrier forgotten (two-argument DestroyEntity)', WP, "ClearCarrierOf(entity);\n        Logs.WriteDebug($\"WorldProxy: entity[{entity.EntityRefId}] -> FlagToDestroy (effectId", "Logs.WriteDebug($\"WorldProxy: entity[{entity.EntityRefId}] -> FlagToDestroy (effectId"),
    ('M16', 'clearing of the carrier forgotten (single-argument DestroyEntity)', WP, "ClearCarrierOf(entity);\n        Logs.WriteDebug($\"WorldProxy: entity[{entity.EntityRefId}] -> FlagToDestroy (search-destroyed)", "Logs.WriteDebug($\"WorldProxy: entity[{entity.EntityRefId}] -> FlagToDestroy (search-destroyed)"),
    ('M17', 'clearing with a test of identity', WP, 'if (entity.PlatformEntity?.GameplayProxy is EntityScriptProxy carrier)\n        {\n            carrier.CarriedEntity = null;', 'if (entity.PlatformEntity?.GameplayProxy is EntityScriptProxy carrier && ReferenceEquals(carrier.CarriedEntity, entity.LogicContextEntity))\n        {\n            carrier.CarriedEntity = null;'),
    ('M18', 'states 0x06 and 0x2E swapped', CR, 'input.DirectionHeld ? JumpWalkState : JumpStillState', 'input.DirectionHeld ? JumpStillState : JumpWalkState'),
    ('M19', 'PutInHands: marker not posed', PM, 'item.HasLogicalZ = true;\n    }\n\n    /// <summary>\n    /// E20.g1a T3 (W1', 'item.HasLogicalZ = item.HasLogicalZ;\n    }\n\n    /// <summary>\n    /// E20.g1a T3 (W1'),
    ('M20', 'PutInHands: RidingEntity not cleared', PM, "item.RidingEntity = null;\n        item.HasLogicalZ", "item.HasLogicalZ"),
    ('M21', 'W6: sort key not raised to the carrier', ED, 'return depthSortValue < carrierKey ? carrierKey : depthSortValue;', 'return depthSortValue;'),
    ('M22', 'W2: mode 1 not called in the locked branch', PM, "            UpdatePlayerCarriedEntity(player, 1);\n            EndOfMovePlayer", "            EndOfMovePlayer"),
    ('M23', 'W1: body of UpdatePlayerCarriedEntity removed', PM, "=> CarryRules.UpdateCarried(player, mode, player.ScriptHost as IEntityWorldContext);", "{ }"),
    ('M24', 'W4: lift gesture branch removed', PM, 'else if (player.TargetAnimationId is CarryRules.LiftGroundState or CarryRules.LiftAirState)', 'else if (false)'),
    ('M25', 'W4: carrying group branch removed', PM, 'else if (CarryRules.IsCarryGroupState(player.TargetAnimationId))', 'else if (false)'),
    ('M26', 'heavy test: any kind bit', PM, 'Heavy: item != null && (item.Flags & EntityFlags.PickupKindMask) == EntityFlags.PickupKindMask,', 'Heavy: item != null && (item.Flags & EntityFlags.PickupKindMask) != 0,'),
    ('M27', 'W3: follow branch removed', ESP, 'if (Status.IsActive() && PlatformEntity?.GameplayProxy is EntityScriptProxy carrier)', 'if (false && Status.IsActive() && PlatformEntity?.GameplayProxy is EntityScriptProxy carrier)'),
    ('M28', 'follow without the Z offset', ESP, 'PosZ = unchecked(carrier.PosZ + carrier.RelativeWarpOffsetZ);', 'PosZ = carrier.PosZ;'),
    ('M29', 'follow without the push to the root', ESP, "        ModdedPosZ = unchecked(PosZ + ModZ);\n        PushLogicalPositionToRoot();\n    }\n\n    /// <summary>\n    /// The hero's tile-attribute", "        ModdedPosZ = unchecked(PosZ + ModZ);\n    }\n\n    /// <summary>\n    /// The hero's tile-attribute"),
    ('M30', 'registration of C23 removed (phase C)', NR, "        LiftableAi.Register(registry);\n", ""),
    ('M31', 'C23: carrier link not cleared at the release', LA, 'carrier.CarriedEntity = null;', 'carrier.CarriedEntity = carrier.CarriedEntity;'),
    ('M32', 'C23: riding not cleared at the release', LA, "entity.RidingEntity = null;\n        entity.Flags", "entity.Flags"),
    ('M33', 'mode 2 not called... swim: mode 2 uses mode 0 branch (mode default removed)', CR, 'default:\n                item.Flags2 = -1;', 'default:\n                item.Flags2 = item.Flags2;'),
]


def run(cmd, timeout=590):
    return subprocess.run(cmd, capture_output=True, text=True, encoding='utf-8', errors='replace', cwd=REPO, timeout=timeout)


only = sys.argv[1:]
for mid, name, path, old, new in MUTS:
    if only and mid not in only:
        continue
    full = os.path.join(REPO, path)
    raw = open(full, 'rb').read()
    text = raw.decode('utf-8')
    crlf = '\r\n' in text
    t = text.replace('\r\n', '\n')
    if t.count(old) != 1:
        line = '%s | %s | NOT APPLIED (count %d)' % (mid, name, t.count(old))
        print(line); open(OUT, 'a').write(line + '\n')
        continue
    mutated = t.replace(old, new, 1)
    if crlf:
        mutated = mutated.replace('\n', '\r\n')
    open(full, 'wb').write(mutated.encode('utf-8'))
    t0 = time.time()
    try:
        b = run(['dotnet', 'build', 'Alundra.Tests/Alundra.Tests.csproj', '-c', 'Release', K, '-v', 'q', '--nologo'])
        if b.returncode != 0:
            line = '%s | %s | BUILD ERROR' % (mid, name)
        else:
            r = run(['dotnet', 'test', 'Alundra.Tests/Alundra.Tests.csproj', '-c', 'Release', K, '--no-build', '--blame-hang-timeout', '60s', '--nologo', '--filter', FILT])
            out = r.stdout
            open(S + '/g1a-t5/log-' + mid + '.txt', 'w', encoding='utf-8').write(out)
            mm = re.search(r'chec\s*:\s*(\d+),\s*r.{1,3}ussite\s*:\s*(\d+)', out)
            nred = int(mm.group(1)) if mm else -1
            names = sorted(set(re.findall(r'^\s+\S*chou\S*\s+(\S+)', out, re.M)))
            short = sorted(set(f.split('.')[-1][:70] for f in names))
            ntot = int(mm.group(2)) + nred if mm else -1
            if r.returncode == 0:
                line = '%s | %s | SURVIVED (%d tests green)' % (mid, name, ntot)
            else:
                line = '%s | %s | KILLED (%d red; first: %s)' % (mid, name, nred, '; '.join(short[:8]))
    finally:
        open(full, 'wb').write(raw)
    line += ' [%ds]' % (time.time() - t0)
    print(line); open(OUT, 'a').write(line + '\n')
