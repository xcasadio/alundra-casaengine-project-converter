"""scen_grass.py - critic: grass cutting by the exploding bomb (E12 -> CheckAndTriggerTileEffect on the 4 corners of the box)."""
from scen_zone import *
import world as W
TILES = W.TILES; ROW = W.ROW
def cell(col, row): return TILES + 0x604 + row * ROW + col * 8
rw = Rigw()
m = rw.m
# grass (walkability bit 2) on a 7x7 block of cells around the bomb cell; height 0
bcol, brow = BX // 24, BY // 16
for c in range(bcol - 3, bcol + 4):
    for r in range(brow - 3, brow + 4):
        a = cell(c, r)
        m.poke16(a, m.peek(a) & 0xFFFF | 2)
rw.snap = (bytes(m.ram), bytes(m.init))
rw.restore()
pad = W.Pad()
b = spawn(m, H0, 0, 2, BX, BY, 0)
m.poke(b + 0x274, 2)
cut = {}
for k in range(1, 70):
    m.tick_no = k
    j0 = len(m.journal)
    W.tick(m, pad, 0, check_pad=False)
    for c in range(bcol - 3, bcol + 4):
        for r in range(brow - 3, brow + 4):
            if (m.peek(cell(c, r)) & 2) == 0 and (c, r) not in cut:
                cut[(c, r)] = k
    new = [x for x in m.journal[j0:] if x[1] in ('effect_3BDD8', 'sound')]
    if new and k > 3:
        print('tick', k, [(x[1], x[2] if x[1] == 'sound' else x[2][:2]) for x in new][:6])
print('bomb cell', (bcol, brow), 'cut cells (col,row)->tick:', cut)
print('count', len(cut), 'of 49')
# first tick the box exists (+0x1D4 != 0) and box fields
