import sys, os
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import pworld as P
import world as W
from summ import timeline
from p8 import steps

def cells(col, row):
    if (col, row) == (7, 6):
        return (0, 0, 0x02, 0)          # cuttable grass flag, wall tile id irrelevant
    return (0, 0, 0, 0)

res = P.run(dict(item=2, n=150, holds={t: W.SQUARE for t in range(1, 96)}, after=0, cells=cells))
prev = res['rng0']
for r in res['rows']:
    ev = [j for j in r['journal'] if not j.startswith('stub')]
    if r['tick'] >= 117 and (ev or r['rng'] != prev):
        print(r['tick'], 'rng steps', steps(prev, r['rng']), ev)
    prev = r['rng']
m = res['m']
cell = W.TILES + 0x604 + 6 * W.ROW + 7 * 8
print('cell word after: %04X +6: %04X' % (m.peek(cell) & 0xFFFF, (m.peek(cell + 4) >> 16) & 0xFFFF))
