# E20.h4d1 T3: real mutations by script, one at a time, applied, suite run (Release, copy neutralised), undone.
# usage: python -B mutants.py <name> [<name> ...]   (prints, per mutant, the failed tests; the files are restored in a finally)
import subprocess, sys, re, os
R = 'D:/development/repo/alundra-casaengine-project-converter'
SK = 'C:/Users/casad/AppData/Local/Temp/claude/D--development-repo-alundra-casaengine-project-converter/b00d1a72-420d-4acc-ab34-0c25fbdad3a1/scratchpad/e20e0a3b-exec/skipcopy.targets'
FM = 'Alundra/Ai/Items/FireTome2Ai.cs'
NE = 'Alundra/Rendering/Effects/NativeEffects.cs'
SW = 'Alundra/Cameras/CameraSway.cs'
RADIUS = '            var radius = EnemyHelpers.Below(BlastRadiusBound) + BlastRadiusBase;\n'
M = {
 'idx_k': [(FM, 'AnimationTables.OffsetXList[4 * k]', 'AnimationTables.OffsetXList[k]'), (FM, 'AnimationTables.OffsetYList[4 * k]', 'AnimationTables.OffsetYList[k]')],
 'no_0x320': [(FM, 'EnemyHelpers.Below(BlastRadiusBound) + BlastRadiusBase;', 'EnemyHelpers.Below(BlastRadiusBound);')],
 'bound_0x1800': [(FM, 'BlastRadiusBound = 0x1801;', 'BlastRadiusBound = 0x1800;')],
 'signed_product': [(FM, 'EnemyHelpers.Below(BlastRadiusBound) + BlastRadiusBase;', '(int)(((long)(int)(uint)Alundra.Rng.GameRandom.Next() * BlastRadiusBound) >> 32) + BlastRadiusBase;')],
 'xy_swap': [(FM, 'entity.PosX + AnimationTables.OffsetXList[4 * k]', 'entity.PosX + AnimationTables.OffsetYList[4 * k]'), (FM, 'entity.PosY + AnimationTables.OffsetYList[4 * k]', 'entity.PosY + AnimationTables.OffsetXList[4 * k]')],
 'z_plus_0x100000': [(FM, 'var z = entity.PosZ + 1;', 'var z = entity.PosZ + 1 + 0x100000;')],
 'z_no_plus1': [(FM, 'var z = entity.PosZ + 1;', 'var z = entity.PosZ;')],
 'anim1': [(NE, 'reservoir.Create(false, FireColumnBlastEffect, 2, x, y, z)', 'reservoir.Create(false, FireColumnBlastEffect, 1, x, y, z)')],
 'effect18': [(NE, 'FireColumnBlastEffect = 19;', 'FireColumnBlastEffect = 18;')],
 'draw_skipped_full': [(FM, RADIUS, '            if (stage is not null && System.Linq.Enumerable.Count(stage.Reservoir.ActiveSlots) >= 128) { continue; }\n' + RADIUS)],
 'destroy_other_effect': [(FM, 'DestroyEntity(entity, -1);', 'DestroyEntity(entity, 8);')],
 'destroy_minus1_removed': [(FM, 'DestroyEntity(entity, -1);', 'DestroyEntity(entity, 0);')],
 'capture_after_flag': [(FM, '        world!.CameraSway?.CaptureToBuffer();\n        hero.ItemUsePhase = 1;\n        return Burn(hero);\n', '        hero.ItemUsePhase = 1;\n        var burn = Burn(hero);\n        world!.CameraSway?.CaptureToBuffer();\n        return burn;\n')],
 'capture_in_phase1': [(FM, '        world!.CameraSway?.CaptureToBuffer();\n', ''), (FM, '                sway.Flag = 1;\n', '                sway.CaptureToBuffer();\n                sway.Flag = 1;\n')],
 'shifts_swapped': [(FM, 'sway.LimitY = sway.SpeedY = counter >> 4;', 'sway.LimitY = sway.SpeedY = counter >> 6;'), (FM, 'sway.LimitX = sway.SpeedX = counter >> 6;', 'sway.LimitX = sway.SpeedX = counter >> 4;')],
 'speed_ne_limit': [(FM, 'sway.LimitY = sway.SpeedY = counter >> 4;', 'sway.LimitY = counter >> 4; sway.SpeedY = counter >> 5;')],
 'identity_status4': [(FM, 'column.Status == EntityStatus.Destroyed ||', 'column.Status == EntityStatus.FlagToDestroy || column.Status == EntityStatus.Destroyed ||')],
 'restore_guarded_nonzero': [(SW, '    internal void RestoreFromBuffer()\n    {\n', '    internal void RestoreFromBuffer()\n    {\n        if (System.Array.TrueForAll(Buffer, v => v == 0)) { return; }\n')],
 'restore_missing_at_death': [(FM, '        sway?.RestoreFromBuffer();\n        hero.TargetAnimationId = PlayerManager.TomeEndAnimationId;\n', '        hero.TargetAnimationId = PlayerManager.TomeEndAnimationId;\n')],
 'no_0x34': [(FM, '        hero.TargetAnimationId = PlayerManager.TomeEndAnimationId;\n', '')],
 'phase2_no_wait': [(FM, 'return hero.TargetAnimationId == PlayerManager.TomeEndAnimationId ? 0 : 1;', 'return 1;')],
 'guard_in_phase2': [(FM, 'return hero.TargetAnimationId == PlayerManager.TomeEndAnimationId ? 0 : 1;', 'return PlayerManager.TomeSlopeGuard(hero) is var g2 && g2 != 0 ? g2 : hero.TargetAnimationId == PlayerManager.TomeEndAnimationId ? 0 : 1;')],
 'cancel_by_table_index': [(FM, 'entity.SpriteType == ColumnType && entity.Status == EntityStatus.Normal', 'entity.SpriteTableIndex == (uint)ColumnType && entity.Status == EntityStatus.Normal')],
 'cancel_statuses_to_3': [(FM, 'entity.SpriteType == ColumnType && entity.Status == EntityStatus.Normal', 'entity.SpriteType == ColumnType && entity.Status != EntityStatus.Destroyed')],
 'blocker_left': [(FM, '                entity.BlockedByEntity = null;\n', '')],
 'birth_late': [(FM, '        var world = hero.ScriptHost as IEntityWorldContext;\n        var column = world?.SpawnEntityBySpriteIndex', '        if (hero.ItemUseCounter == 32) { return 0; }\n        var world = hero.ScriptHost as IEntityWorldContext;\n        var column = world?.SpawnEntityBySpriteIndex')],
 'offsets_reach_not_restored': [(SW, '        OffsetX = Buffer[5];\n        OffsetY = Buffer[6];\n        ReachX = Buffer[7];\n        ReachY = Buffer[8];\n', '')],
 'pointer_after_null_test': [(FM, '        hero.ItemSpawnedEntity = column;\n        if (column is null)\n        {\n            return 1;\n        }\n', '        if (column is null)\n        {\n            return 1;\n        }\n\n        hero.ItemSpawnedEntity = column;\n')],
}
FLT = 'FullyQualifiedName~FireTome2|FullyQualifiedName~CameraSwayBuffer|FullyQualifiedName~NativeSpriteRegistryTests|FullyQualifiedName~LiftableAiRegistrationTests|FullyQualifiedName~ChargedArrowAiTests|FullyQualifiedName~WaveSwordLifeTests|FullyQualifiedName~BallFlamesTests'
outdir = os.path.dirname(os.path.abspath(__file__))
for name in sys.argv[1:]:
    saved = {}
    for path, old, new in M[name]:
        full = R + '/' + path
        if path not in saved:
            saved[path] = open(full, encoding='utf-8', newline='').read()
        s = open(full, encoding='utf-8', newline='').read()
        nl = '\r\n' if '\r\n' in s else '\n'
        o = old.replace('\n', nl); n = new.replace('\n', nl)
        assert s.count(o) == 1, (name, path, old[:50])
        open(full, 'w', encoding='utf-8', newline='').write(s.replace(o, n))
    try:
        out = subprocess.run(['dotnet', 'test', R + '/Alundra.Tests/Alundra.Tests.csproj', '-c', 'Release', '-p:CustomAfterMicrosoftCommonTargets=' + SK, '--filter', FLT, '--blame-hang-timeout', '60s'], capture_output=True, text=True, encoding='utf-8', errors='replace').stdout
    finally:
        for path, s in saved.items():
            open(R + '/' + path, 'w', encoding='utf-8', newline='').write(s)
    failed = sorted(set(re.findall(r'Échoué (Alundra\.Tests\S+)', out)))
    errs = sorted(set(re.findall(r'error CS\d+: [^\[]*', out)))[:3]
    res = [l.strip() for l in out.splitlines() if 'Échoué!' in l or 'Réussi!' in l]
    with open(os.path.join(outdir, 'out_' + name + '.txt'), 'w', encoding='utf-8') as f:
        f.write('mutant ' + name + '\nfailed ' + str(len(failed)) + '\n' + '\n'.join(' ' + x for x in failed) + '\n' + '\n'.join(errs) + '\n' + '\n'.join(res) + '\n')
    print(name, len(failed), errs)
    for x in failed:
        print('   ', x.replace('Alundra.Tests.', '')[:110])
    print('  ', res[:1])
