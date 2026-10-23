"""t5_mutants.py apply|restore <name> | list: real mutations of the DLL code of E20.e0c (T5, the whole slice), applied by script and undone byte for byte from a backup in the scratchpad."""
import io, os, shutil, sys

R = 'D:/development/repo/alundra-casaengine-project-converter/'
B = 'C:/Users/casad/AppData/Local/Temp/claude/D--development-repo-alundra-casaengine-project-converter/b00d1a72-420d-4acc-ab34-0c25fbdad3a1/scratchpad/mutbak5/'
P = 'Alundra/Ai/Objects/PickupAi.cs'
W = 'Alundra/Maps/WorldProxy.cs'
L = 'Alundra/Items/LootDrop.cs'
I = 'Alundra/Items/ItemEffects.cs'
H = 'Alundra/Hero/HeroHeals.cs'

# name: (file, [(old, new), ...])   (anchors are written with LF; the file's own line endings are kept; each anchor must occur exactly once)
MUTANTS = {
    # --- the drop (LootDrop)
    'veto_forgotten_contents': (L, [("        var item = unchecked((int)container.ContentsItemId);\n        if (item == 0 || Veto(item, state))", "        var item = unchecked((int)container.ContentsItemId);\n        if (item == 0)")]),
    'veto_forgotten_random': (L, [("out _);\n        if (item == 0 || Veto(item, state))", "out _);\n        if (item == 0)")]),
    'veto_before_the_roll': (L, [("        var item = (int)LootRoll.Choose(", "        if (state.PlayerStats.MpMax == 0)\n        {\n            return false;\n        }\n\n        var item = (int)LootRoll.Choose(")]),
    'life_always_600': (L, [("tables != null && (uint)item < ItemTables.DropRecordCount && tables.DropField1[item] != 0 ? DropLifeTicks : -1;", "DropLifeTicks;")]),
    'drop_z_not_minus_one': (L, [("unchecked(binaryZ - 1)", "binaryZ")]),
    'drop_sound_omitted': (L, [("        world.SoundPlayer?.PlaySfx(DropSound);\n        return true;", "        return true;")]),
    # --- the destruction (WorldProxy.DestroyEntity)
    'drop_after_the_status_4': (W, [("        LootDrop.SpawnContents(this, GameState, entity);\n        entity.Status = EntityStatus.FlagToDestroy;\n", "        entity.Status = EntityStatus.FlagToDestroy;\n        LootDrop.SpawnContents(this, GameState, entity);\n")]),
    'drop_in_the_light_destruction': (W, [("    internal void DestroyEntity(EntityScriptProxy entity)\n    {\n        entity.Status = EntityStatus.FlagToDestroy;", "    internal void DestroyEntity(EntityScriptProxy entity)\n    {\n        LootDrop.SpawnContents(this, GameState, entity);\n        entity.Status = EntityStatus.FlagToDestroy;")]),
    # --- the bounce, the life, the contact (C255)
    'bounce_threshold_le': (P, [("if (!((entity.MapGravityRaw << 8) < bounce))", "if (!((entity.MapGravityRaw << 8) <= bounce))")]),
    'bounce_without_reset': (P, [("            if (!((entity.MapGravityRaw << 8) < bounce))\n            {\n                WriteWord284(entity, 0);\n            }\n", "")]),
    'life_destroys_at_1': (P, [("            if (life == 0)\n            {\n                world.DestroyEntity(entity);", "            if (life == 1)\n            {\n                world.DestroyEntity(entity);")]),
    'life_mark_omitted': (P, [("                entity.DamagedTickCounter = LifeMarkValue;\n", "")]),
    'overlap_hero_le': (P, [("distance >= 0 ? distance < unchecked(heroSize + 1)", "distance >= 0 ? distance <= unchecked(heroSize + 1)")]),
    'overlap_object_le': (P, [(": unchecked(heroPosition - objectPosition) < unchecked(objectSize + 1);", ": unchecked(heroPosition - objectPosition) <= unchecked(objectSize + 1);")]),
    'overlap_positions_of_the_current_tick': (P, [("OverlapsOnAxis(entity.ModdedPosX, hero.ModdedPosX,", "OverlapsOnAxis(entity.PosX + entity.ModX, hero.PosX + hero.ModX,")]),
    'pickup_during_a_dialogue': (P, [("if (DialogueIsOpen(world) || world.PlayerEntity is not { } hero", "if (world.PlayerEntity is not { } hero")]),
    'state3_does_not_wait_for_the_box': (P, [("                if (DialogueIsOpen(world))\n                {\n                    return;\n                }\n\n                world.Unblock", "                world.Unblock")]),
    # --- the message, the sound, the modes, the masks, the states
    'message_omitted_for_the_placed': (P, [("var message = ItemEffects.ComposePickupMessage(item, kind, giveReturn);", "var message = kind == KindPlaced ? string.Empty : ItemEffects.ComposePickupMessage(item, kind, giveReturn);")]),
    'message_present_for_the_dropped': (P, [("var message = ItemEffects.ComposePickupMessage(item, kind, giveReturn);", "var message = ItemEffects.ComposePickupMessage(item, KindPlaced, giveReturn);")]),
    'key_and_ordinary_swapped': (P, [("        if (sound != 0)\n        {\n            world.SoundPlayer", "        if (sound == 0)\n        {\n            world.SoundPlayer")]),
    'modes_0_and_1_swapped': (P, [("private const int OrdinaryMessageMode = 0;", "private const int OrdinaryMessageMode = 1;"), ("private const int KeyMessageMode = 1;", "private const int KeyMessageMode = 0;")]),
    'mask_4_not_written': (P, [("        world.DialogueDirector?.SetCloseMask(ScriptCloseOnlyMask);\n", "")]),
    'mask_3_not_written': (P, [("                world.DialogueDirector?.SetCloseMask(DefaultCloseMask);\n", "")]),
    'ordinary_reaches_the_state_2': (P, [("            OpenMessage(world, message, OrdinaryMessageMode);\n            entity.ItemState += 2;", "            OpenMessage(world, message, OrdinaryMessageMode);\n            entity.ItemState += 1;")]),
    'set_game_flag_minus_1_ported': (P, [("        if (kind != KindPlaced)\n        {\n            SetGameFlag(state, ReadWord280(entity));\n        }\n", "        SetGameFlag(state, ReadWord280(entity));\n")]),
    'fanfare_waits_for_cd_busy': (P, [("private static bool FanfareIsBusy() => false;", "private static bool FanfareIsBusy() => true;")]),
    # --- A255
    'a255_without_not_0x80': (P, [("        entity.Flags &= ~EntityFlags.Collidable;\n", "")]),
    'a255_registered_for_the_chest': (P, [("internal static readonly int[] SpriteTypes = Enumerable.Range(31, 118 - 31 + 1)", "internal static readonly int[] SpriteTypes = Enumerable.Range(30, 118 - 30 + 1)")]),
    # --- Block / Unblock and the entities that stand still
    'block_forgotten': (P, [("        world.Block(entity);\n", "")]),
    'block_without_the_hero': (W, [("        var hero = PlayerEntity;\n        if (hero != null)\n        {\n            yield return hero;\n        }\n", "        var hero = PlayerEntity;\n")]),
    'unblock_forgotten': (P, [("                world.Unblock(entity);\n", "")]),
    'blocked_entity_runs_its_clock': (W, [("            if (_tickEntities[i].BlockedByEntity == null)\n            {\n                _tickEntities[i].TickAnimationClock();\n            }\n", "            _tickEntities[i].TickAnimationClock();\n")]),
    'blocked_entity_runs_its_physics': (W, [("            if (_tickEntities[i].BlockedByEntity == null)\n            {\n                _tickEntities[i].TickPhysics();\n            }\n", "            _tickEntities[i].TickPhysics();\n")]),
    # --- GiveItem and the heals
    'money_cap_removed': (I, [("            case 0x47:\n                PlayerManager.AddMoney(state, 10);", "            case 0x47:\n                state.PlayerStats.Money = (short)(state.PlayerStats.Money + 10);")]),
    'medium_heart_at_half_of_hp_max': (H, [("Math.Max(1, hero.HpMax / 4)", "Math.Max(1, hero.HpMax / 2)")]),
    'sparks_created_at_once': (H, [("        stage.Emit(reservoir => creators(reservoir, x, y, z));", "        creators(stage.Reservoir, x, y, z);")]),
    'inventory_first_inverted': (I, [("        return before < PlayerManager.GetNumberOfItem(state, item);", "        return before >= PlayerManager.GetNumberOfItem(state, item);")]),
}


def rd(p):
    with io.open(R + p, encoding='utf-8', newline='') as f:
        return f.read()


def main():
    mode = sys.argv[1]
    if mode == 'list':
        print('\n'.join(MUTANTS))
        return
    name = sys.argv[2]
    file, repl = MUTANTS[name]
    bak = B + file.replace('/', '__')
    if mode == 'apply':
        os.makedirs(B, exist_ok=True)
        shutil.copyfile(R + file, bak)
        s = rd(file)
        crlf = '\r\n' in s
        t = s.replace('\r\n', '\n')
        for old, new in repl:
            assert t.count(old) == 1, (name, 'anchor not found exactly once', t.count(old))
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
