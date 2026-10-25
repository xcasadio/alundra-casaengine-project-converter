"""E20.g2b2 T3 - real mutations of the line of the hook and of the rule it feeds (applied to the working tree, undone by `git checkout -- Alundra/`).
usage: python -B mut.py <name>      (applies the mutant; asserts every pattern matches exactly once)
       python -B mut.py --list
"""
import sys

REPO = 'D:/development/repo/alundra-casaengine-project-converter/'
EP = 'Alundra/Entities/EntityScriptProxy.cs'
WP = 'Alundra/Maps/WorldProxy.cs'

M = {}


def mut(name, *edits):
    M[name] = edits


HOOK = '                if (HasLogicalZ)\n                {\n                    UpdateVramFlags();\n                }\n'
OUTER = 'if (HasLogicalZ || Alundra.Ai.Objects.LiftableAi.FeedsGroundSlope(SpriteType))'

# --- the six of the plan ------------------------------------------------------------------------------------------------------------
mut('01_and_instead_of_or_in_the_pick_phase', (EP, '(CombinedVramFlagsOR & 0x8004U) != 0', '(CombinedVramFlagsAND & 0x8004U) != 0'))
mut('01b_and_instead_of_or_in_the_word', (EP, 'CombinedVramFlagsOR = corner0 | corner1 | corner2 | corner3;', 'CombinedVramFlagsOR = corner0 & corner1 & corner2 & corner3;'))
mut('02_mask_0004_only', (EP, '(CombinedVramFlagsOR & 0x8004U) != 0', '(CombinedVramFlagsOR & 0x0004U) != 0'))
mut('03_no_gravity_gate', (EP, 'void UpdateVramFlags()\n    {\n        if ((Flags & EntityFlags.Gravity) == 0)', 'void UpdateVramFlags()\n    {\n        if (false)'))
mut('04_hook_for_every_entity_inner', (EP, HOOK, '                {\n                    UpdateVramFlags();\n                }\n'))
mut('04b_hook_for_every_entity_outer_and_inner', (EP, HOOK, '                {\n                    UpdateVramFlags();\n                }\n'), (EP, OUTER, 'if (true)'))
mut('05_call_before_the_step', (EP, '            ScriptedMotion.TickScriptedNpc(this);\n', '            if (HasLogicalZ) { UpdateVramFlags(); }\n            ScriptedMotion.TickScriptedNpc(this);\n'),
    (EP, HOOK, ''))
mut('06_break_effect_lost_effect_id_0', (EP, '                        host.DestroyEntity(this, -1);\n                        eventProgramType = ScriptHelper.ProgramUnknown;\n                    }\n                    else if (((flags & EntityFlags.DeactivateOnImpact)',
    '                        host.DestroyEntity(this, 0);\n                        eventProgramType = ScriptHelper.ProgramUnknown;\n                    }\n                    else if (((flags & EntityFlags.DeactivateOnImpact)'))
mut('06b_break_effect_lost_in_the_world', (WP, 'var fx = effectId == -1 ? entity.BreakEffect : effectId;', 'var fx = effectId == -1 ? 0 : effectId;'))
# --- more than the plan -------------------------------------------------------------------------------------------------------------
mut('07_and_word_as_or', (EP, 'CombinedVramFlagsAND = corner0 & corner1 & corner2 & corner3;', 'CombinedVramFlagsAND = corner0 | corner1 | corner2 | corner3;'))
mut('08_three_corners', (EP, '        CombinedVramFlagsOR = corner0 | corner1 | corner2 | corner3;', '        CombinedVramFlagsOR = corner0 | corner1 | corner2;'))
mut('09_hook_removed', (EP, HOOK, ''))
mut('10_mask_8000_only', (EP, '(CombinedVramFlagsOR & 0x8004U) != 0', '(CombinedVramFlagsOR & 0x8000U) != 0'))
mut('11_hook_only_when_normal', (EP, HOOK, '                if (HasLogicalZ && Status == EntityStatus.Normal)\n                {\n                    UpdateVramFlags();\n                }\n'))

if sys.argv[1] == '--list':
    for k in M:
        print(k)
    sys.exit(0)
for path, a, b in M[sys.argv[1]]:
    p = REPO + path
    s = open(p, encoding='utf-8', newline='').read()
    if '\r\n' in s:
        a = a.replace('\n', '\r\n')
        b = b.replace('\n', '\r\n')
    assert s.count(a) == 1, (sys.argv[1], path, s.count(a))
    open(p, 'w', encoding='utf-8', newline='').write(s.replace(a, b))
print('applied', sys.argv[1])
