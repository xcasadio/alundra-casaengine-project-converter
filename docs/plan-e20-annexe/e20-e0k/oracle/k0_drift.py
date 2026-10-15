"""k0_drift.py - cell drift (conveyor / wind) forces: end pass 0x80038064 computes +0x188 from the cell word under the entity's pixel tile, 0x800366FC adds the tables 0x80028B94 / 0x80028BD4 to the horizontal force.
The cell word (slope byte = bits 16..23): bit 6 (0x400000) or bit 7 (0x800000) must be set; bit 2 (0x40000) enables the drift when the entity's ModdedPosZ == cell height*16 px + 1 raw (standing), bit 3 (0x80000) when it is not;
bits 4-5 = direction code d, +0x188 = 1 << d (| 0x80 when bit 7).  Run: python -B -I k0_drift.py"""
import sys, os
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import k0lib as K
import interp2
from k0_arrows import OUT
from k0_walker import walker
from k0_arrows2 import arrow_run

lines = []
for d in range(4):
    for en, nm in ((0x04, 'ground-drift bit 2'), (0x08, 'air-drift bit 3')):
        sb = 0x40 | en | (d << 4)
        cells = lambda col, row, sb=sb: dict(slope=sb) if col == 4 else None
        m, s, init, rows = walker(cells, anim=0, n=6, pos=(100, 100))         # col 4 = x 96..119: the Beannoid at rest (anim 0, no speed)
        m2, s2, init2, rows2 = walker(cells, anim=0, n=6, pos=(100, 100), ticker=interp2.tick2)
        dd = K.same_rows(rows, rows2)
        r = rows[-1]['slots'][s]
        lines.append('walker at rest on cell slope byte 0x%02X (%s, dir %d): +0x188 = %X ; displacement over 6 ticks (X, Y px) = (%.4f, %.4f) ; interp diff %s' % (sb, nm, d, r['p188'], (r['px'] - init['px']) / 65536, (r['py'] - init['py']) / 65536, dd))
# arrow over the cell
for en, nm in ((0x04, 'ground-drift bit 2'), (0x08, 'air-drift bit 3')):
    sb = 0x40 | en | (1 << 4)
    cells = lambda col, row, sb=sb: dict(slope=sb) if 5 <= col <= 6 else None
    m, s, init, rows = arrow_run(cells, typ=3, n=22, z_px=16)
    ys = [round(r['slots'][s]['py'] / 65536, 3) for r in rows[:22]]
    xs = [round(r['slots'][s]['px'] / 65536, 3) for r in rows[:22]]
    lines.append('arrow (flying 16 px above) over cells col 5..6 slope byte 0x%02X (%s dir 1): +0x188 values seen %s ; Y per tick %s' % (sb, nm, sorted(set('%X' % r['slots'][s]['p188'] for r in rows)), ys))
txt = '\n'.join(lines)
open(os.path.join(OUT, 'drift-summary.txt'), 'w').write(txt + '\n')
print(txt)
