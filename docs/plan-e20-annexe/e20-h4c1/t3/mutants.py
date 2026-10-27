# E20.h4c1 T3: real mutations applied by script, the targeted suite run (Release, neutralised copy), then undone; `git diff` empty after each.
# Usage: python -B mutants.py <name> [<name> ...]   (or "all")
import subprocess, sys, re, time
R = 'D:/development/repo/alundra-casaengine-project-converter'
SK = 'C:/Users/casad/AppData/Local/Temp/claude/D--development-repo-alundra-casaengine-project-converter/b00d1a72-420d-4acc-ab34-0c25fbdad3a1/scratchpad/e20e0a3b-exec/skipcopy.targets'
AI = 'Alundra/Ai/Items/EarthTome1Ai.cs'
PM = 'Alundra/Hero/PlayerManager.cs'
M = {
 'm01_speed2_1024': [(AI, 'rock.ForceZ = speed << 8;', 'rock.ForceZ = (speed > 1024 ? 1024 : speed) << 8;')],
 'm02_boundary_60_to_65': [(AI, 'rock.ForceZ = speed << 8;', 'rock.ForceZ = (tick >= 65 ? speed : 1024) << 8;')],
 'm03_one_birth_a_tick': [(AI, 'if (tick == counter)\n', 'if (tick == counter && (index == 0 || Table[index - 1].Tick != counter))\n')],
 'm04_tick_20_to_25': [(AI, '0x14, 0x00, 0x00, 0x04, 0x03, 0x00,', '0x19, 0x00, 0x00, 0x04, 0x03, 0x00,')],
 'm05_tick_95_to_90': [(AI, '0x5F, 0x00, 0x00, 0x05, 0x1E, 0x00,', '0x5A, 0x00, 0x00, 0x05, 0x1E, 0x00,')],
 'm06_delta_sign': [(AI, 'hero.TargetDirection + (uint)delta', 'hero.TargetDirection - (uint)delta')],
 'm07_delta_permuted': [(AI, '0x14, 0x00, 0x00, 0x04, 0x03, 0x00,\n        0x14, 0x00, 0x00, 0x04, 0xFA, 0xFF,', '0x14, 0x00, 0x00, 0x04, 0xFA, 0xFF,\n        0x14, 0x00, 0x00, 0x04, 0x03, 0x00,')],
 'm08_boxdirection_base': [(AI, 'hero.TargetDirection + (uint)delta', '(uint)hero.BoxDirection + (uint)delta')],
 'm09_no_and31': [(AI, ') & 31)', '))')],
 'm10_shift7': [(AI, 'rock.ForceZ = speed << 8;', 'rock.ForceZ = speed << 7;')],
 'm11_null_parent': [(AI, 'SpawnEntityBySpriteIndex(hero, false,', 'SpawnEntityBySpriteIndex(null, false,')],
 'm12_forcez_on_null_birth': [(AI, 'rock.ForceZ = speed << 8;\n                }', 'rock.ForceZ = speed << 8;\n                }\n                else\n                {\n                    hero.ForceZ = speed << 8;\n                }')],
 'm13_null_birth_dereferenced': [(AI, 'if (rock is not null)', 'if (rock is not null || true)')],
 'm14_threshold_99': [(AI, 'private const int EndCounter = 100;', 'private const int EndCounter = 99;')],
 'm15_threshold_101': [(AI, 'private const int EndCounter = 100;', 'private const int EndCounter = 101;')],
 'm16_guard_accepts_34': [(PM, 'DeepWaterSlope && player.TargetAnimationId is TomeCastAnimationId or TomeHoldAnimationId)', 'DeepWaterSlope && player.TargetAnimationId is TomeCastAnimationId or TomeHoldAnimationId or TomeEndAnimationId)')],
 'm17_guard_ignores_slope': [(PM, 'if (player.Slope_18c != DeepWaterSlope && player.TargetAnimationId is TomeCastAnimationId', 'if (player.TargetAnimationId is TomeCastAnimationId')],
 'm18_phase1_guard_removed': [(AI, '    private static int Hold(EntityScriptProxy hero)\n    {\n        var guard = PlayerManager.TomeSlopeGuard(hero);\n        if (guard != 0)\n        {\n            return guard;\n        }\n', '    private static int Hold(EntityScriptProxy hero)\n    {\n')],
 'm19_phase2_never_ends': [(AI, 'PlayerManager.TomeEndAnimationId ? 0 : 1;', 'PlayerManager.TomeEndAnimationId ? 0 : 0;')],
 'm20_phase2_ends_at_once': [(AI, 'PlayerManager.TomeEndAnimationId ? 0 : 1;', 'PlayerManager.TomeEndAnimationId ? 1 : 1;')],
 'm21_table_32bit_words': [(AI, 'entries[i] = (BitConverter.ToUInt16(TableBytes, at), BitConverter.ToInt16(TableBytes, at + 2), BitConverter.ToInt16(TableBytes, at + 4));', 'entries[i] = ((int)BitConverter.ToUInt32(TableBytes, at), BitConverter.ToInt16(TableBytes, at + 2), BitConverter.ToInt16(TableBytes, at + 4));')],
 'm22_e3_no_or40': [(AI, '(entity.Flags | 0x40u) & ~0x30u', 'entity.Flags & ~0x30u')],
 'm23_e3_clears_only_10': [(AI, '(entity.Flags | 0x40u) & ~0x30u', '(entity.Flags | 0x40u) & ~0x10u')],
 'm24_e3_no_end_test': [(AI, 'entity.TargetAnimationId == ImpactAnimation && entity.ForceResetAnimationFlag == 1', 'entity.TargetAnimationId == ImpactAnimation')],
 'm25_e3_registered_for_17': [(AI, 'SpriteTypes = { 16 };', 'SpriteTypes = { 16, 17 };')],
 'm26_e3_registered_for_16_23': [(AI, 'SpriteTypes = { 16 };', 'SpriteTypes = { 16, 23 };')],
 'm27_cancel_not_neutral': [(AI, 'internal static void Cancel(EntityScriptProxy hero)\n    {\n', 'internal static void Cancel(EntityScriptProxy hero)\n    {\n        hero.ItemUsePhase = 1;\n        hero.TargetAnimationId = PlayerManager.TomeEndAnimationId;\n')],
 'm28_cancel_writes_force': [(AI, 'internal static void Cancel(EntityScriptProxy hero)\n    {\n', 'internal static void Cancel(EntityScriptProxy hero)\n    {\n        hero.ForceZ = 1;\n')],
 'e01_equivalent_destroy_0': [(AI, 'DestroyEntity(entity, -1)', 'DestroyEntity(entity, 0)')],
}
FLT = 'FullyQualifiedName~EarthTome1|FullyQualifiedName~NativeSpriteRegistryTests|FullyQualifiedName~LiftableAiRegistrationTests|FullyQualifiedName~ChargedArrowAiTests|FullyQualifiedName~WaveSwordLifeTests|FullyQualifiedName~BallFlamesTests|FullyQualifiedName~HeroItemUseTests|FullyQualifiedName~Tome'
names = sorted(M) if sys.argv[1:] == ['all'] else sys.argv[1:]
for name in names:
    t0 = time.time()
    saved = {}
    out = ''
    try:
        for path, old, new in M[name]:
            full = R + '/' + path
            cur = open(full, encoding='utf-8', newline='').read()
            saved.setdefault(path, cur)
            nl = '\r\n' if '\r\n' in cur else '\n'
            o = old.replace('\n', nl)
            n = new.replace('\n', nl)
            assert cur.count(o) == 1, (name, path, cur.count(o))
            open(full, 'w', encoding='utf-8', newline='').write(cur.replace(o, n))
        out = subprocess.run(['dotnet', 'test', R + '/Alundra.Tests/Alundra.Tests.csproj', '-c', 'Release', '-p:CustomAfterMicrosoftCommonTargets=' + SK, '--filter', FLT, '--blame-hang-timeout', '60s'], capture_output=True, text=True, encoding='utf-8', errors='replace').stdout
    finally:
        for path, s in saved.items():
            open(R + '/' + path, 'w', encoding='utf-8', newline='').write(s)
    diff = subprocess.run(['git', '-C', R, 'diff', '--quiet', '--', AI, PM]).returncode
    failed = sorted(set(re.findall(r'Échoué (Alundra\.Tests\S+)', out)))
    build = 'error CS' in out
    summ = [l.strip() for l in out.splitlines() if 'Échoué!' in l or 'Réussi!' in l]
    print('MUTANT', name, 'BUILD_ERROR' if build else ('KILLED' if failed else 'SURVIVED'), 'red_cases=%d' % len(failed), 'git_diff_empty=%s' % (diff == 0), '%ds' % (time.time() - t0))
    for f in failed[:6]:
        print('   ', f)
    print('   ', summ[:1])
    sys.stdout.flush()
