"""E20.g2c1 T4 - real mutations of C4 and its line (applied to the working tree, undone by `git checkout -- Alundra/`).
usage: python -B mut.py <name>      (applies the mutant; asserts the pattern matches exactly once)
       python -B mut.py --list
"""
import sys

REPO = 'D:/development/repo/alundra-casaengine-project-converter/'
LA = 'Alundra/Ai/Objects/LiftableAi.cs'
CT = 'Alundra/Physics/CornerTileWords.cs'
EP = 'Alundra/Entities/EntityScriptProxy.cs'

M = {}


def mut(name, path, a, b):
    M[name] = (path, a, b)


# --- the list of the plan (14 items) ----------------------------------------------------------------------------------------------
mut('01_forcez_ge0', LA, 'entity.ForceZ > 0 && entity.DelayOrAngleOrEntityId == 0', 'entity.ForceZ >= 0 && entity.DelayOrAngleOrEntityId == 0')
mut('02_lock_not_set', LA, '                        entity.DelayOrAngleOrEntityId = 1;\n', '')
mut('03_direction_step_08', LA, 'BounceDirectionStep = 0x10;', 'BounceDirectionStep = 0x08;')
mut('04_effect_lift_16px', LA, 'BounceEffectLift = 8 << 16;', 'BounceEffectLift = 16 << 16;')
mut('05_mask_1000', CT, 'Mask = 0x1001;', 'Mask = 0x1000;')
mut('06_three_corners', CT, 'var corners = new[] { (x1, y1), (x2, y1), (x1, y2), (x2, y2) };', 'var corners = new[] { (x1, y1), (x2, y1), (x1, y2) };')
mut('07_hitcounter_ignored', LA, 'entity.IsOnGround != 0 || entity.HitCounter != 0', 'entity.IsOnGround != 0')
mut('08_sound_other', LA, 'LandingSfx = 0x18;', 'LandingSfx = 0x19;')
mut('09_slope_1', LA, 'TrunkWaterSlope = 4;', 'TrunkWaterSlope = 1;')
mut('10_float_animation_4', LA, 'TrunkFloatAnimation = 5;', 'TrunkFloatAnimation = 4;')
mut('11_native_e_test_removed', LA, 'throwKind == -1 && entity.SpriteProgramIndexes[ScriptHelper.ProgramEDeactivate] == C4DeathFlagsNativeCode ?', 'throwKind == -1 ?')
mut('12_death_bits_30', LA, 'C4DeathSetBits = 0x10;', 'C4DeathSetBits = 0x30;')
mut('13_c23_takes_c4_policy', LA, 'TickCarried(entity, ReleaseFlagsPolicy.C23);', 'TickCarried(entity, ReleaseFlagsPolicy.C4);')
mut('14_feeds_slope_for_all', LA, 'FeedsGroundSlope(int spriteType) => spriteType == TrunkType;', 'FeedsGroundSlope(int spriteType) => true;')
mut('15_wired_type_without_key', LA, 'RegisterC4Production(NativeSpriteRegistry registry) => RegisterC4(registry, C4ProductionTypes);',
    'RegisterC4Production(NativeSpriteRegistry registry) => RegisterC4(registry, C4ProductionTypes.Where(t => t != 369));')
# --- more than the plan -------------------------------------------------------------------------------------------------------------
mut('16_anymask_nonzero', CT, 'if ((word & Mask) == Mask)', 'if ((word & Mask) != 0)')
mut('17_trunk_float_exemption_removed', LA, 'if (animation != 0 && !(entity.SpriteType == TrunkType && animation == TrunkFloatAnimation))', 'if (animation != 0)')
mut('18_status3_not_set', LA, '                        entity.Status = EntityStatus.Deactivated;\n', '                        _ = entity.Status;\n')
mut('19_carried_uses_c23_policy', LA, 'TickCarried(entity, ReleaseFlagsPolicy.C4);\n            return;', 'TickCarried(entity, ReleaseFlagsPolicy.C23);\n            return;')
mut('20_lock_not_cleared_on_ground', LA, '                entity.DelayOrAngleOrEntityId = 0;\n', '')
mut('21_blocked_ground_anim_kept', LA, '                    entity.TargetAnimationId = 0;\n                }\n            }\n            else if', '                }\n            }\n            else if')
mut('22_glide_ignored', LA, 'if (!CornerTileWords.AnyHasMask(readCorners(entity)))\n                {\n                    entity.TargetAnimationId = 0;', 'if (true)\n                {\n                    entity.TargetAnimationId = 0;')
mut('23_trunk_slope_not_resets', LA, 'else if (entity.TargetAnimationId == TrunkFloatAnimation && entity.Slope_18c != TrunkWaterSlope)', 'else if (false)')
mut('24_feeds_slope_wrong_type', LA, 'spriteType == TrunkType;', 'spriteType == 293;')
mut('25_effect_9_other', LA, 'BounceEffect = 9;', 'BounceEffect = 8;')

if sys.argv[1] == '--list':
    for k in M:
        print(k)
    sys.exit(0)
path, a, b = M[sys.argv[1]]
p = REPO + path
s = open(p, encoding='utf-8', newline='').read()
crlf = '\r\n' in s
if crlf:
    a = a.replace('\n', '\r\n')
    b = b.replace('\n', '\r\n')
assert s.count(a) == 1, (sys.argv[1], s.count(a))
open(p, 'w', encoding='utf-8', newline='').write(s.replace(a, b))
print('applied', sys.argv[1])
