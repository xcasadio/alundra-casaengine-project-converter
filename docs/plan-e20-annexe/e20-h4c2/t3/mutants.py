# E20.h4c2 T3: real mutations applied by script, the targeted suite run (Release, neutralised copy), then undone; `git diff` empty after each.
# Usage: python -B mutants.py <name> [<name> ...]   (or "all")
import subprocess, sys, re, time
R = 'D:/development/repo/alundra-casaengine-project-converter'
SK = 'C:/Users/casad/AppData/Local/Temp/claude/D--development-repo-alundra-casaengine-project-converter/b00d1a72-420d-4acc-ab34-0c25fbdad3a1/scratchpad/e20e0a3b-exec/skipcopy.targets'
AI = 'Alundra/Ai/Items/EarthTome2Ai.cs'
CM = 'Alundra/Cameras/CameraMath.cs'
WP = 'Alundra/Maps/WorldProxy.cs'
SM = 'Alundra/Physics/ScriptedMotion.cs'
PM = 'Alundra/Hero/PlayerManager.cs'
NS = 'Alundra/Ai/NativeSpriteRegistry.cs'
FILES = [AI, CM, WP, SM, PM, NS]
GUARD_BLOCK = '        if (entity.IsOnGround == 0)\n        {\n            entity.Status = EntityStatus.Normal;\n            return;\n        }\n\n'
M = {
 'm01_r1_r2_swapped': [(AI, 'var place = EnemyHelpers.Below(32);\n                var direction = EnemyHelpers.Below(32);', 'var direction = EnemyHelpers.Below(32);\n                var place = EnemyHelpers.Below(32);')],
 'm02_direction_on_r1': [(AI, 'hero.PosZ + BirthHeight, direction);', 'hero.PosZ + BirthHeight, place);')],
 'm03_no_a00': [(AI, 'var distance = p + ExtraDistance;', 'var distance = p;')],
 'm04_x32_y32_swapped': [(AI, 'hero.PosX + AnimationTables.OffsetXList[place] * distance, hero.PosY + AnimationTables.OffsetYList[place] * distance', 'hero.PosX + AnimationTables.OffsetYList[place] * distance, hero.PosY + AnimationTables.OffsetXList[place] * distance')],
 'm05_birth_height_128px': [(AI, 'private const int BirthHeight = 0x1000000;', 'private const int BirthHeight = 0x800000;')],
 'm06_force_z_minus_4px': [(AI, 'unchecked((int)0xFFF80000)', 'unchecked((int)0xFFFC0000)')],
 'm07_destroy_le': [(AI, 'unchecked(heroPosZ + 1 + BirthHeight) < terrain', 'unchecked(heroPosZ + 1 + BirthHeight) <= terrain')],
 'm08_terrain_without_plus1': [(AI, 'unchecked(heroPosZ + 1 + BirthHeight) < terrain', 'unchecked(heroPosZ + BirthHeight) < terrain')],
 'm09_null_birth_destroys': [(AI, 'rock.ForceZ = FallForce;\n                    }\n                }', 'rock.ForceZ = FallForce;\n                    }\n                }\n                else if (world is not null)\n                {\n                    world.DestroyEntity(hero);\n                }')],
 'm10_entry_224_shifted': [(AI, '0xE0, 0x00, 0x00, 0x1A,', '0xD0, 0x00, 0x00, 0x1A,')],
 'm11_one_draw': [(AI, 'var direction = EnemyHelpers.Below(32);', 'var direction = place;')],
 'm12_phase1_guard_removed': [(AI, 'private static int Hold(EntityScriptProxy hero)\n    {\n        var guard = PlayerManager.TomeSlopeGuard(hero);\n        if (guard != 0)\n        {\n            return guard;\n        }\n\n', 'private static int Hold(EntityScriptProxy hero)\n    {\n')],
 'm13_threshold_0x119': [(AI, 'private const int EndCounter = 0x118;', 'private const int EndCounter = 0x119;')],
 'm14_phase2_ends_at_once': [(AI, 'PlayerManager.TomeEndAnimationId ? 0 : 1;', 'PlayerManager.TomeEndAnimationId ? 1 : 1;')],
 'm15_phase1_not_chained': [(AI, 'if (index >= EntryCount)\n        {\n            hero.ItemUsePhase = 1;', 'if (index > EntryCount)\n        {\n            hero.ItemUsePhase = 1;')],
 'm16_e13_without_kick': [(AI, '(entity.ScriptHost as IEntityWorldContext)?.SetCameraKick(KickScroll);', '// mutant: kick removed')],
 'm17_kick_added': [(WP, '_cameraKickOfTick[_cameraKickTick] = scrollY;', '_cameraKickOfTick[_cameraKickTick] += scrollY;')],
 'm18_without_0x40': [(AI, '(entity.Flags | 0x40u) & ~0x30u', 'entity.Flags & ~0x30u')],
 'm19_0x30_not_cleared': [(AI, '(entity.Flags | 0x40u) & ~0x30u', '(entity.Flags | 0x40u)')],
 'm20_destroy_effect_0': [(AI, 'DestroyEntity(entity, -1)', 'DestroyEntity(entity, 0)')],
 'm20b_destroy_single_argument': [(AI, 'DestroyEntity(entity, -1)', 'DestroyEntity(entity)')],
 'm21_kick_at_first_view_of_cancelled': [(AI, '            return;\n        }\n\n        // D-H4C2-3', '        }\n\n        // D-H4C2-3')],
 'm22_cancel_all_types': [(AI, 'entity.SpriteType == SpriteTypes[0] && entity.Status == EntityStatus.Normal', 'entity.Status == EntityStatus.Normal')],
 'm23_cancel_touches_status3': [(AI, 'entity.SpriteType == SpriteTypes[0] && entity.Status == EntityStatus.Normal', 'entity.SpriteType == SpriteTypes[0] && (entity.Status == EntityStatus.Normal || entity.Status == EntityStatus.Deactivated)')],
 'm24_cancel_forgets_highest_slot': [(AI, 'foreach (var entity in world.SpawnedEntities)', 'foreach (var entity in System.Linq.Enumerable.Take(world.SpawnedEntities, world.SpawnedEntities.Count - 1))')],
 'm25_kick_sign_inverted': [(CM, '(sway?.OffsetY ?? 0) + kick);', '(sway?.OffsetY ?? 0) - kick);')],
 'm26_kick_applied_at_snap_tick': [(CM, '                firstFullTick = 1;\n', '                firstFullTick = 1;\n                if (cameraKickOfTick is { Count: > 0 })\n                {\n                    smoothed.Y -= cameraKickOfTick[0];\n                }\n')],
 'm27_kick_filed_by_image': [(WP, '_cameraKickOfTick[_cameraKickTick] = scrollY;', '_cameraKickOfTick[0] = scrollY;')],
 'm28_kick_consumed_one_tick_late': [(CM, 'var kick = cameraKickOfTick is not null && kickIndex < cameraKickOfTick.Count ? cameraKickOfTick[kickIndex] : 0;', 'var kick = cameraKickOfTick is not null && kickIndex >= 1 && kickIndex - 1 < cameraKickOfTick.Count ? cameraKickOfTick[kickIndex - 1] : 0;')],
 'm29_index_shift_at_snap': [(CM, 'firstFullTick = 1;\n', 'firstFullTick = 0;\n')],
 'm30_e13_also_for_469': [(AI, 'internal static readonly int[] SpriteTypes = { 17 };', 'internal static readonly int[] SpriteTypes = { 17, 469 };')],
 'm31_dF55_rule_removed': [(SM, '|| entity.HasLogicalZ ? entity.TargetAnimationId', '? entity.TargetAnimationId')],
 'm32_speed_target_forgotten_for_17': [(SM, '|| entity.HasLogicalZ ? entity.TargetAnimationId', '|| (entity.HasLogicalZ && entity.SpriteType != 17) ? entity.TargetAnimationId')],
 'm33_e13_without_ground_guard': [(AI, GUARD_BLOCK, '')],
 'm34_guard_on_force_adjusted': [(AI, 'if (entity.IsOnGround == 0)', 'if (entity.ForceAdjusted != 0)')],
 'm35_status_left_at_3': [(AI, 'entity.Status = EntityStatus.Normal;\n            return;', 'return;')],
 'm36_guard_on_second_branch': [(AI, 'if (entity.ForceResetAnimationFlag == 1)', 'if (entity.ForceResetAnimationFlag == 1 && entity.IsOnGround != 0)')],
 'm37_kick_value_3': [(AI, 'private const int KickScroll = 2;', 'private const int KickScroll = 3;')],
 'm38_phase1_end_does_not_enter_phase2': [(AI, 'hero.ItemUsePhase = 2;', 'hero.ItemUsePhase = 1;')],
 'm39_cancel_keeps_blocker': [(AI, '                entity.BlockedByEntity = null;\n', '')],
 'm40_cancel_without_animation': [(AI, '                entity.TargetAnimationId = ImpactAnimation;\n            }\n        }\n    }', '            }\n        }\n    }')],
 'm41_kick_list_not_emptied': [(WP, '        _cameraKickOfTick.Clear(); // E20.h4c2', '        // E20.h4c2')],
 'm42_kick_tick_not_reset': [(WP, '        _cameraKickTick = 0;\n', '')],
 'm43_kick_tick_not_set_in_block': [(WP, '_cameraKickTick = tick; // E20.h4c2', '// E20.h4c2')],
 'm44_cancel_entry_to_stub': [(PM, 'EarthTome2Ai.Cancel(player);', 'ItemCancelStub(item);')],
 'm45_driver_entry_to_stub': [(PM, 'item == EarthTome2Item ? EarthTome2Ai.Driver(player)', 'item == EarthTome2Item ? ItemDriverStub(item)')],
 'm46_registration_removed': [(NS, '        EarthTome2Ai.Register(registry);\n', '')],
 'e01_equivalent_no_resync': [(AI, '        entity.ResyncControllerFromFlags();\n', '')],
}
FLT = 'FullyQualifiedName~EarthTome|FullyQualifiedName~CameraKick|FullyQualifiedName~NativeSpriteRegistryTests|FullyQualifiedName~LiftableAiRegistrationTests|FullyQualifiedName~ChargedArrowAiTests|FullyQualifiedName~WaveSwordLifeTests|FullyQualifiedName~BallFlamesTests|FullyQualifiedName~HeroItemUseTests|FullyQualifiedName~Tome'
names = sorted(M) if sys.argv[1:] == ['all'] else sys.argv[1:]
if names == ['baseline']:
    M['baseline'] = []
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
    diff = subprocess.run(['git', '-C', R, 'diff', '--quiet', '--'] + FILES).returncode
    failed = sorted(set(re.findall(r'(?:\S*chou\S*|Failed) (Alundra\.Tests\S+)', out)))
    build = 'error CS' in out
    m = re.search(r'chec\S*\s*:\s*(\d+)', out)
    nfail = int(m.group(1)) if m else -1
    summ = [l.strip() for l in out.splitlines() if 'total' in l and 'Alundra.Tests.dll' in l]
    print('MUTANT', name, 'BUILD_ERROR' if build else ('KILLED' if (failed or nfail > 0) else 'SURVIVED'), 'red_cases=%d' % max(len(failed), nfail), 'git_diff_empty=%s' % (diff == 0), '%ds' % (time.time() - t0))
    for f in failed[:5]:
        print('   ', f)
    print('   ', summ[:1])
    sys.stdout.flush()
