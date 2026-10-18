"""make_listings.py - E20.h1 discovery: my OWN capstone listings of every function the report cites (ALUN_CD.EXE France, SHA-1 a705f8da...), one file per function in ./lst/, plus the jump tables.
Read-only on the binary.  usage: python -B make_listings.py"""
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
from bdis import dis, u32, u16     # noqa: E402

OUT = os.path.join(HERE, 'lst')
os.makedirs(OUT, exist_ok=True)

FUNCS = [
    ('tryuseitem_8002ED64', 0x8002ED64, 0x8002EDBC, 'TryUseItem: the Circle front, the control flags, the dispatcher'),
    ('dispatch_8003499C', 0x8003499C, 0x80034ACC, 'the start dispatcher: promoted item, refusals, jump table 0x8002339C, the in-use words'),
    ('start_herb_80034680', 0x80034680, 0x800346F0, 'I36 Herb start'),
    ('start_lpotion_800346F0', 0x800346F0, 0x80034760, 'I37 L Potion start'),
    ('start_elixir_80034760', 0x80034760, 0x800347D4, 'I38 Magic Elixir start'),
    ('start_cureale_800347D4', 0x800347D4, 0x80034870, 'I39 Cure Ale start (also used by the death branch, E18)'),
    ('start_spotion_80034870', 0x80034870, 0x800348E0, 'I41 S Potion start'),
    ('heal_herb_80032E50', 0x80032E50, 0x80032EEC, 'Hp += max(5, HpMax/4), effect 14'),
    ('heal_water1_80032F84', 0x80032F84, 0x800330FC, 'Hp += max(5, HpMax/2), four sparks (water 1)'),
    ('heal_spotion_800330FC', 0x800330FC, 0x80033274, 'Hp += max(10, HpMax/2), four sparks (S Potion)'),
    ('heal_lpotion_80033274', 0x80033274, 0x800333AC, 'Hp = HpMax, eight rings (L Potion, water 2)'),
    ('heal_elixir_800336F0', 0x800336F0, 0x8003382C, 'Mp = MpMax, eight rings of animation 2'),
    ('heal_cureale_80033A2C', 0x80033A2C, 0x80033D34, 'Hp = HpMax and Mp = MpMax, eight rings, eight sparks'),
    ('heal_small_80032E2C', 0x80032E2C, 0x80032E50, 'Hp = min(HpMax, Hp + 2), no effect (loot pickup 0x54)'),
    ('heal_quarter_80032EEC', 0x80032EEC, 0x80032F84, 'Hp += max(1, HpMax/4), one effect 14 (loot pickup 0x55)'),
    ('useitem_8004E5C4', 0x8004E5C4, 0x8004E640, 'UseItem: one less of the item'),
    ('getnumberofitem_8004E428', 0x8004E428, 0x8004E484, 'GetNumberOfItem'),
    ('setitemid_8004E0F8', 0x8004E0F8, 0x8004E168, 'SetItemIdFromCurrentItemId (promotion of the Circle item)'),
    ('getmpmax_8004DDDC', 0x8004DDDC, 0x8004DDF4, 'GetPlayerMpMax'),
    ('getmp_8004DEA4', 0x8004DEA4, 0x8004DEBC, 'GetPlayerMp'),
    ('setmp_8004DEBC', 0x8004DEBC, 0x8004DF10, 'SetPlayerMp'),
    ('sethp_8004DD30', 0x8004DD30, 0x8004DD80, 'SetPlayerHp'),
    ('createeffect_8003BDD8', 0x8003BDD8, 0x8003BE74, 'CreateEffectEntity(0, id, animation, x, y, z)'),
    ('initeffect_8003BB14', 0x8003BB14, 0x8003BBDC, 'InitEffect: the template copy and the position'),
    ('getfreeeffect_8003B9C4', 0x8003B9C4, 0x8003B9FC, 'GetFreeEffect: 128 slots of 0x80 bytes from 0x80138608'),
    ('frame_8002BAEC', 0x8002BAEC, 0x8002BD60, 'the per-frame function: Start+Select counter (0x8002BB64-0x8002BC4C), Start menu request (0x8002BC58-0x8002BD04), the generator step (0x8002BD0C-0x8002BD34)'),
    ('pickup_loot_80033DBC', 0x80033DBC, 0x80033EC8, 'loot pickup effects by id 0x45-0x56 (C255 0x8007C358)'),
    ('pickup_item_80033F00', 0x80033F00, 0x80034110, 'item pickup: a healing item that does not fit the bag is used at once (C255 0x8007C598)'),
]
TABLES = [
    ('jumptable_8002339C', 0x8002339C, 20, 'start jump table, index = item - 0x1F'),
    ('jumptable_pickup_loot_8002308C', 0x8002308C, 18, 'loot pickup jump table, index = id - 0x45'),
    ('jumptable_pickup_item_800230D4', 0x800230D4, 0x57, 'item pickup jump table, index = item'),
]


def main():
    for name, lo, hi, what in FUNCS:
        with open(os.path.join(OUT, name + '.txt'), 'w') as f:
            f.write('# %s  [%08x, %08x)  %s\n# my capstone listing of ALUN_CD.EXE (France): address  word  instruction\n' % (name, lo, hi, what))
            f.write('\n'.join(dis(lo, hi)) + '\n')
    for name, base, n, what in TABLES:
        with open(os.path.join(OUT, name + '.txt'), 'w') as f:
            f.write('# %s  %s\n' % (name, what))
            for i in range(n):
                f.write('%08x  [%d] %08x\n' % (base + 4 * i, i, u32(base + 4 * i)))
    with open(os.path.join(OUT, 'halfword_tables_80023654_80023694.txt'), 'w') as f:
        f.write('# the two 32-entry halfword tables of the rings: 0x80023654 (768 * cos) and 0x80023694 (512 * sin)\n')
        for base in (0x80023654, 0x80023694):
            vals = []
            for i in range(32):
                v = u16(base + 2 * i)
                vals.append(v - 0x10000 if v & 0x8000 else v)
            f.write('%08x: %s\n' % (base, vals))
    print('wrote %d listings and %d tables to %s' % (len(FUNCS), len(TABLES) + 1, OUT))


if __name__ == '__main__':
    main()
