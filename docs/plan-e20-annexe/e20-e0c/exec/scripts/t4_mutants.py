"""t4_mutants.py apply|restore <name>: real mutations of the DLL code of phase D (E20.e0c T4), applied by script and undone byte for byte from a backup in the scratchpad."""
import io, os, shutil, sys

R = 'D:/development/repo/alundra-casaengine-project-converter/'
B = 'C:/Users/casad/AppData/Local/Temp/claude/D--development-repo-alundra-casaengine-project-converter/b00d1a72-420d-4acc-ab34-0c25fbdad3a1/scratchpad/mutbak4/'
P = 'Alundra/Ai/Objects/PickupAi.cs'
W = 'Alundra/Maps/WorldProxy.cs'

MUTANTS = {
    # name: (file, old, new)   (the anchors are written with LF; the file's own line endings are kept)
    'message_omitted_for_the_placed': (P, "var message = ItemEffects.ComposePickupMessage(item, kind, giveReturn);", "var message = kind == KindPlaced ? string.Empty : ItemEffects.ComposePickupMessage(item, kind, giveReturn);"),
    'message_for_the_dropped': (P, "var message = ItemEffects.ComposePickupMessage(item, kind, giveReturn);", "var message = ItemEffects.ComposePickupMessage(item, KindPlaced, giveReturn);"),
    'key_and_ordinary_swapped': (P, "        if (sound != 0)\n        {\n            world.SoundPlayer", "        if (sound == 0)\n        {\n            world.SoundPlayer"),
    'ordinary_mode_1': (P, "private const int OrdinaryMessageMode = 0;", "private const int OrdinaryMessageMode = 1;"),
    'key_mode_0': (P, "private const int KeyMessageMode = 1;", "private const int KeyMessageMode = 0;"),
    'mask4_omitted': (P, "        world.DialogueDirector?.SetCloseMask(ScriptCloseOnlyMask);\n", ""),
    'mask3_omitted': (P, "                world.DialogueDirector?.SetCloseMask(DefaultCloseMask);\n", ""),
    'mask3_is_4': (P, "private const int DefaultCloseMask = 3;", "private const int DefaultCloseMask = 4;"),
    'ordinary_reaches_state2': (P, "            OpenMessage(world, message, OrdinaryMessageMode);\n            entity.ItemState += 2;", "            OpenMessage(world, message, OrdinaryMessageMode);\n            entity.ItemState += 1;"),
    'block_omitted': (P, "        world.Block(entity);\n", ""),
    'ordinary_blocks': (P, "            world.SoundPlayer?.PlaySfx(sound);\n            OpenMessage(world, message, OrdinaryMessageMode);", "            world.Block(entity);\n            world.SoundPlayer?.PlaySfx(sound);\n            OpenMessage(world, message, OrdinaryMessageMode);"),
    'fanfare_start_omitted': (P, "        FanfareStart();\n", ""),
    'fanfare_end_omitted': (P, "                FanfareEnd();\n", ""),
    'fanfare_waits': (P, "private static bool FanfareIsBusy() => false;", "private static bool FanfareIsBusy() => true;"),
    'flag_minus1_ported': (P, "        if (kind != KindPlaced)\n        {\n            SetGameFlag(state, ReadWord280(entity));\n        }\n", "        SetGameFlag(state, ReadWord280(entity));\n"),
    'a255_collidable_kept': (P, "        entity.Flags &= ~EntityFlags.Collidable;\n", ""),
    'a255_clears_for_the_dropped': (P, "        if (ReadWord274(entity) != KindPlaced)\n        {\n            return;\n        }\n\n        entity.Flags", "        entity.Flags"),
    'a255_flag_branch_proceeds': (P, "        if (entity.ContentsGameFlag != 0)\n        {\n            return; // NAMED STUB (D-E0C-3).\n        }\n\n", ""),
    'a255_word280_zero': (P, "WriteWord280(entity, uint.MaxValue);", "WriteWord280(entity, 0);"),
    'a255_state_1': (P, "        entity.ItemState = StateRise;\n        WriteWord280", "        entity.ItemState = StatePickup;\n        WriteWord280"),
    'a255_life_not_written': (P, "        entity.DelayOrAngleOrEntityId = entity.ContentsGameFlag;\n", ""),
    'chest_runs': (P, "if (ReadWord274(entity) is not (KindPlaced or KindDropped))", "if (ReadWord274(entity) is not (KindPlaced or KindDropped or 2u))"),
    'registered_for_chest': (P, "internal static readonly int[] SpriteTypes = Enumerable.Range(31, 118 - 31 + 1)", "internal static readonly int[] SpriteTypes = Enumerable.Range(30, 118 - 30 + 1)"),
    'clock_of_a_blocked_entity_runs': (W, "            if (_tickEntities[i].BlockedByEntity == null)\n            {\n                _tickEntities[i].TickAnimationClock();\n            }\n", "            _tickEntities[i].TickAnimationClock();\n"),
    'physics_of_a_blocked_entity_runs': (W, "            if (_tickEntities[i].BlockedByEntity == null)\n            {\n                _tickEntities[i].TickPhysics();\n            }\n", "            _tickEntities[i].TickPhysics();\n"),
    'pickup_in_dialogue': (P, "if (DialogueIsOpen(world) || world.PlayerEntity is not { } hero", "if (world.PlayerEntity is not { } hero"),
    'sound_omitted': (P, "            world.SoundPlayer?.PlaySfx(sound);\n", ""),
    'give_omitted': (P, "        var giveReturn = ItemEffects.GiveItem(world, state, hero, item);", "        var giveReturn = 0;"),
    'unblock_omitted': (P, "                world.Unblock(entity);\n", ""),
}


def rd(p):
    with io.open(R + p, encoding='utf-8', newline='') as f:
        return f.read()


def main():
    mode, name = sys.argv[1], sys.argv[2]
    file, old, new = MUTANTS[name]
    bak = B + file.replace('/', '__')
    if mode == 'apply':
        os.makedirs(B, exist_ok=True)
        shutil.copyfile(R + file, bak)
        s = rd(file)
        crlf = '\r\n' in s
        t = s.replace('\r\n', '\n')
        assert t.count(old) == 1, (name, 'anchor not found once', t.count(old))
        t = t.replace(old, new)
        if crlf:
            t = t.replace('\n', '\r\n')
        with io.open(R + file, 'w', encoding='utf-8', newline='') as f:
            f.write(t)
        print('applied', name)
    else:
        shutil.copyfile(bak, R + file)
        print('restored', name)


if __name__ == '__main__':
    main()
