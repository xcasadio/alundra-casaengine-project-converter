"""E20.g1b T4 - real mutations of the lift (applied to the working tree, undone by `git checkout`).
usage: python -B mut.py <name>      (applies the mutant; asserts the pattern matches exactly once)
       python -B mut.py --list
"""
import sys

REPO = 'D:/development/repo/alundra-casaengine-project-converter/'
PM = 'Alundra/Hero/PlayerManager.cs'
EP = 'Alundra/Entities/EntityScriptProxy.cs'
PAD = 'PadState.Square'

M = {}


def mut(name, path, a, b):
    M[name] = (path, a, b)


# --- the list of the plan (12 items) -------------------------------------------------------------------------------------------------
mut('01_genre23_without_glove', PM, '(int)kind >= 2 && !ownsPowerGlove', 'false')
mut('02_glove_inverted', PM, '(int)kind >= 2 && !ownsPowerGlove', '(int)kind >= 2 && ownsPowerGlove')
mut('03_square_mask', PM, 'hero.XCollisionEntity is { } contact && (pad.ButtonsJustPressed & PadState.Square) != 0',
    'hero.XCollisionEntity is { } contact && (pad.ButtonsJustPressed & PadState.Cross) != 0')
mut('04_loop_with_button', PM, 'foreach (var entity in entities)',
    'foreach (var entity in (pad.ButtonsJustPressed & PadState.Square) != 0 ? entities : System.Array.Empty<EntityScriptProxy>())')
mut('05_riding_ignored', PM, '|| !ReferenceEquals(entity.RidingEntity, self))', '|| false)')
mut('06_status_widened', PM, 'entity.Status is not (EntityStatus.Normal or EntityStatus.Deactivated)',
    'entity.Status is not (EntityStatus.Normal or EntityStatus.Deactivated or EntityStatus.FlagToDestroy)')
mut('07_contact_return_lost', PM, 'return TryLift(hero, contact, ownsPowerGlove, wiredTypes);',
    'TryLift(hero, contact, ownsPowerGlove, wiredTypes); return 0;')
mut('08a_set_extended_barrel34', PM,
    'new System.Collections.Generic.HashSet<int>(Alundra.Ai.Objects.LiftableAi.SpriteTypes);',
    'new System.Collections.Generic.HashSet<int>(Alundra.Ai.Objects.LiftableAi.SpriteTypes) { 34 };')
mut('08b_set_extended_torch393', PM,
    'new System.Collections.Generic.HashSet<int>(Alundra.Ai.Objects.LiftableAi.SpriteTypes);',
    'new System.Collections.Generic.HashSet<int>(Alundra.Ai.Objects.LiftableAi.SpriteTypes) { 393 };')
mut('09_contact_never_cleared', EP, '        XCollisionEntity = null;\n        var actual = Controller.Move(requested);',
    '        var actual = Controller.Move(requested);')
mut('10_nil_equals_nil_guard_removed', PM, 'if (self == null)', 'if (false)')
mut('11_interaction_before_lift', PM,
    '            if (TryUseItem(player, in pad) != 0 && PlayerTryAction(player, in pad) == 0)\n            {\n                var interact = CheckEntityInteraction(player, in pad, state, host);\n                if (interact == 2)\n                {\n                    player.TargetAnimationId = IdleAnimationId;\n                }\n                else if (interact == 0)\n                {\n                    ApplyGroundOrAirTail(player, in pad, buttonsHold);\n                }\n            }',
    '            if (TryUseItem(player, in pad) != 0)\n            {\n                var interact = CheckEntityInteraction(player, in pad, state, host);\n                if (interact == 2)\n                {\n                    player.TargetAnimationId = IdleAnimationId;\n                }\n                else if (interact == 0 && PlayerTryAction(player, in pad) == 0)\n                {\n                    ApplyGroundOrAirTail(player, in pad, buttonsHold);\n                }\n            }')
mut('12_climb_calls_removed', PM,
    'if (TryUseItem(player, in pad) == 0 || PlayerTryAction(player, in pad) != 0)\n            {\n            }\n            else if (buttonsHold != 0 && dir != 0 && dir != 0x10)',
    'if (buttonsHold != 0 && dir != 0 && dir != 0x10)')
# --- more than the plan -----------------------------------------------------------------------------------------------------------------
mut('13_empty_set', PM, 'new System.Collections.Generic.HashSet<int>(Alundra.Ai.Objects.LiftableAi.SpriteTypes);',
    'new System.Collections.Generic.HashSet<int>();')
mut('14_stub_body', PM, 'return PlayerTryActionCore(player, in pad, entities, owns, WiredLiftableTypes);',
    '_ = entities; _ = owns; return 0;')
mut('15_wired_check_removed', PM, ' || !wiredTypes.Contains(contact.SpriteType))', ')')
mut('16_notliftable_check_removed', PM, 'if (kind == EntityPickupKind.NotLiftable || !wiredTypes.Contains(contact.SpriteType))',
    'if (!wiredTypes.Contains(contact.SpriteType))')
mut('17_carried_returns_0', PM, '''CarryRules.CarryStillState;
            return 1;''', '''CarryRules.CarryStillState;
            return 0;''')
mut('18_wrong_item_id', PM, 'GetNumberOfItem(host.GameState, 0x3B) != 0', 'GetNumberOfItem(host.GameState, 0x3C) != 0')
mut('19_blocked_by_ignored', PM, '|| entity.BlockedByEntity != null', '|| false')
mut('20_genre_threshold', PM, '(int)kind >= 2 && !ownsPowerGlove', '(int)kind >= 3 && !ownsPowerGlove')
mut('21_jump_states_call_removed', PM,
    '            player.TargetDirection = dir;\n            if (TryUseItem(player, in pad) != 0 && PlayerTryAction(player, in pad) == 0)\n            {\n                ApplyGroundOrAirTail',
    '            player.TargetDirection = dir;\n            if (TryUseItem(player, in pad) != 0)\n            {\n                ApplyGroundOrAirTail')
mut('22_idle_call_removed', PM,
    'if (TryUseItem(player, in pad) != 0 && PlayerTryAction(player, in pad) == 0)\n            {\n                var interact',
    'if (TryUseItem(player, in pad) != 0)\n            {\n                var interact')
mut('23_loop_lifts_without_trylift_answer', PM, 'if (TryLift(hero, entity, ownsPowerGlove, wiredTypes) != 0)',
    'if (TryLift(hero, entity, ownsPowerGlove, wiredTypes) == 0)')
mut('24_air_carry_state_swapped', PM, 'hero.TargetAnimationId = IsHeroInTheAir(hero) ? CarryRules.AirStillState : CarryRules.CarryStillState;',
    'hero.TargetAnimationId = CarryRules.CarryStillState;')

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
