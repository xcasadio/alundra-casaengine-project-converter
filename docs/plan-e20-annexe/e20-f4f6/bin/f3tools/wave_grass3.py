"""wave_grass3.py - bin agent: the Z window of CheckAndTriggerTileEffect on the wave.  Hero stands on height byte 1 (16 px).  Grass cells: columns 5-6 at height 0 (below), 7-8 at height 2 (32 px, above),
rest height 1.  The spy attributes every cut to the calling entity.  Real code (pworld)."""
import sys, os
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import pworld as P
import c1world as C
from machine import s32, M32
from wave_run import run_wave
OUT = os.path.join(HERE, '..', 'out')
ADDR = 0x8003A7B0
CUTS = []

def install_spy(m):
    def spy(mm):
        a0 = mm.r[4]
        slot = (a0 - P.ENT0) // P.STRIDE
        j0 = len(mm.journal)
        h = mm.hooks.pop(ADDR)
        box = (mm.peeks(a0 + 0x1FC), mm.peeks(a0 + 0x200), mm.peeks(a0 + 0x204), mm.peeks(a0 + 0x214), mm.peeks(a0 + 0x218), mm.peeks(a0 + 0x21C))
        v0 = C.side_call(mm, ADDR, [a0], ((mm.r[29] - 0x800) & ~7))
        mm.hooks[ADDR] = h
        for j in mm.journal[j0:]:
            if j[1] == 'effect_3BDD8':
                a = j[2]
                CUTS.append((mm.tick_no, slot, s32(a[3]) / 65536.0, s32(a[4]) / 65536.0, s32(a[5]) / 65536.0, box))
        mm.r[2] = v0
    m.hooks[ADDR] = spy

def cells(c, r):
    if c in (5, 6):
        return (0, 0, 2, 0)
    if c in (7, 8):
        return (2, 0, 2, 0)
    return (1, 0, 2, 0) if c <= 4 or c >= 9 else (1, 0, 0, 0)

if __name__ == '__main__':
    res = run_wave('g', direction='east', n=150, cells=cells, init=[install_spy])
    lines = ['# hero on height byte 1 (rawZ %d); grass cells: cols 5-6 height 0, cols 7-8 height 2, others height 1' % res['rows'][100]['slots'][0]['pz']]
    for t, slot, x, y, z, box in CUTS:
        col, row = int((x - 12) / 24), int((y - 8) / 16)
        lines.append('  t%03d slot %d cut cell(col=%d,row=%d) cellZ=%.0f px  | caller HitBox z=%.5f size z=%.5f' % (t, slot, col, row, z, box[2] / 65536.0, box[5] / 65536.0))
    open(os.path.join(OUT, 'wave_grass_zwindow.txt'), 'w').write('\n'.join(lines) + '\n')
    print('\n'.join(lines))
