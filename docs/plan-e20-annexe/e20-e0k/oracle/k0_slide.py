"""k0_slide.py - the wall slide of a non-hero entity WITHOUT NoObstacleSlide (a ClassA walker, Beannoid anim 5 = speed 256) against a vertical wall column (col 15, x >= 360) and a horizontal wall row (row 12, y >= 192),
for the 32 directions of 0x8C.  Output: for each direction the displacement per tick of the last 10 ticks before and after the contact and the ForceAdjusted ticks.
Run: python -B -I k0_slide.py"""
import sys, os
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import k0lib as K
import interp2
from k0_walker import walker
from k0_arrows import OUT

wallv = lambda col, row: dict(wk=0x40) if col == 15 else None
wallh = lambda col, row: dict(wk=0x40) if row == 12 else None
lines = []
for nm, cells, pos, dirs in (('vertical wall col 15 (x>=360), start (330,100)', wallv, (330, 100), (24, 25, 26, 27, 28, 29, 30, 31, 23, 22, 21, 20, 19, 18, 17)),
                             ('horizontal wall row 12 (y>=192), start (100,172)', wallh, (100, 172), (0, 1, 2, 3, 4, 5, 6, 7, 31, 30, 29, 28, 27, 26, 25)),
                             ('vertical wall col 4 (x<120 free, wall is x 96..119) approached from the east, start (150,100)', lambda col, row: dict(wk=0x40) if col == 4 else None, (150, 100), (8, 9, 10, 11, 12, 13, 14, 15, 7, 6, 5, 4, 3, 2, 1))):
    lines.append('== ' + nm)
    for dr in dirs:
        a = walker(cells, anim=5, direction=dr, n=40, pos=pos)
        b = walker(cells, anim=5, direction=dr, n=40, pos=pos, ticker=interp2.tick2)
        dd = K.same_rows(a[3], b[3])
        m, s, init, rows = a
        adj = [r['tick'] for r in rows if r['slots'][s]['adj_13C']]
        fin = rows[-1]['slots'][s]
        dx = (fin['px'] - init['px']) / 65536
        dy = (fin['py'] - init['py']) / 65536
        lines.append('  dir %2d: ForceAdjusted ticks %s ; displacement after 40 ticks (%.2f, %.2f) ; last-5-tick steps %s ; interp diff %s' % (
            dr, adj[:6] + (['...'] if len(adj) > 6 else []), dx, dy, [(round((rows[i]['slots'][s]['px'] - rows[i - 1]['slots'][s]['px']) / 65536, 3), round((rows[i]['slots'][s]['py'] - rows[i - 1]['slots'][s]['py']) / 65536, 3)) for i in range(35, 40)], dd))
txt = '\n'.join(lines)
open(os.path.join(OUT, 'slide-summary.txt'), 'w').write(txt + '\n')
print(txt)
