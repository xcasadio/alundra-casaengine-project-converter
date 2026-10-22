"""mut.py apply|restore <name>: real mutations of the DLL code of phase B, applied by script and undone byte for byte from a backup in the scratchpad."""
import io, os, shutil, sys

R = 'D:/development/repo/alundra-casaengine-project-converter/'
B = 'C:/Users/casad/AppData/Local/Temp/claude/D--development-repo-alundra-casaengine-project-converter/b00d1a72-420d-4acc-ab34-0c25fbdad3a1/scratchpad/mutbak/'
P = 'Alundra/Ai/Objects/PickupAi.cs'
W = 'Alundra/Maps/WorldProxy.cs'

MUTANTS = {
    # name: (file, old, new)
    'noregister': ('Alundra/Ai/NativeSpriteRegistry.cs', "        PickupAi.Register(registry);\n", ""),
    'threshold_le': (P, "if (!((entity.MapGravityRaw << 8) < bounce))", "if (!((entity.MapGravityRaw << 8) <= bounce))"),
    'bounce_noreset': (P, "            if (!((entity.MapGravityRaw << 8) < bounce))\n            {\n                WriteWord284(entity, 0);\n            }\n", ""),
    'bounce_13': (P, "ReadWord284(entity) * 12u", "ReadWord284(entity) * 13u"),
    'destroy_at_1': (P, "            if (life == 0)\n            {\n                world.DestroyEntity(entity);", "            if (life == 1)\n            {\n                world.DestroyEntity(entity);"),
    'mark_omitted': (P, "                entity.DamagedTickCounter = LifeMarkValue;\n", ""),
    'life_neg_counts': (P, "if (life != -1 && life != 0)", "if (life != 0)"),
    'overlap_hero_le': (P, "distance >= 0 ? distance < unchecked(heroSize + 1)", "distance >= 0 ? distance <= unchecked(heroSize + 1)"),
    'overlap_item_le': (P, ": unchecked(heroPosition - objectPosition) < unchecked(objectSize + 1);", ": unchecked(heroPosition - objectPosition) <= unchecked(objectSize + 1);"),
    'pickup_in_dialogue': (P, "if (DialogueIsOpen(world) || world.PlayerEntity is not { } hero", "if (world.PlayerEntity is not { } hero"),
    'state3_ignores_dialogue': (P, "                if (DialogueIsOpen(world))\n                {\n                    return;\n                }\n\n                world.Unblock", "                world.Unblock"),
    'rise_ge': (P, "                if (entity.ForceZ > 0)", "                if (entity.ForceZ >= 0)"),
    'state_plus1': (P, "entity.ItemState += 2;", "entity.ItemState += 1;"),
    'sound_omitted': (P, "        world.SoundPlayer?.PlaySfx(sound);\n", ""),
    'flag_omitted': (P, "        SetGameFlag(state, ReadWord280(entity));\n", ""),
    'give_omitted': (P, "        ItemEffects.GiveItem(world, state, hero, item);\n", ""),
    'unblock_omitted': (P, "                world.Unblock(entity);\n", ""),
    'placed_runs': (P, "if (ReadWord274(entity) != KindDropped)", "if (ReadWord274(entity) == 2)"),
    'no_moddedpos': (W, "                entity.ModdedPosX = entity.PosX + entity.ModX;\n                entity.ModdedPosY = entity.PosY + entity.ModY;\n                entity.ModdedPosZ = entity.PosZ + entity.ModZ;\n", ""),
    'registered_for_chest': (P, "internal static readonly int[] SpriteTypes = Enumerable.Range(31, 118 - 31 + 1)", "internal static readonly int[] SpriteTypes = Enumerable.Range(30, 118 - 30 + 1)"),
    'sound_zero_proceeds': (P, "        if (sound == 0)\n        {\n            return; // NAMED STUB (phase D): the key objects.\n        }\n", ""),
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


main()
