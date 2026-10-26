"""verify_hero.py - E20.h3a T0 (c): what the hero of the oracle benches (w1) to (w6) carries: the armour word of g_playerStats (+0xC), the kit of the new game (the items AddItem was called for),
HP, MaxHp and the position, before any tick; and the distance to the bombs of each key.  usage (from a copy of critic/run beside the a362 k_*.py): python -B verify_hero.py"""
import world as W
from scen_lib import *
from scen_zone import spawn
m, STATS = mk(None, indices=(1, 2, 253))
H = W.ent(0)
print('armour word g_playerStats+0xC = %d (0 = item 0, not owned: GetArmor -1)' % m.peeks16(STATS + 0xC) if hasattr(m, 'peeks16') else 'armour word = %d' % (m.peek(STATS + 0xC) & 0xFFFF))
print('hero hp %d, x %.2f, y %.2f' % (m.peeks(H + 0x14), m.peeks(H + 0x114) / 65536, m.peeks(H + 0x118) / 65536))
inv = [(i, (m.peek(STATS + 0x12 + 4 * i))) for i in range(128) if m.peek(STATS + 0x12 + 4 * i)]
print('inventory slots non-zero:', [(i, hex(v)) for i, v in inv])
cats = {i: (m.peek(0x800C5F7C + 8 * i + 4) >> 16) & 0x7F for i, _ in inv}
print('categories (byte +6 & 0x7F, 2 = armour) of the inventory items:', cats)
print('owned item of category 2 in the inventory:', [i for i, c in cats.items() if c == 2])
