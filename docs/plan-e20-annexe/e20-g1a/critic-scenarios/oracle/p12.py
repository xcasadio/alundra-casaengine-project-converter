import sys, os
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import pworld as P
import world as W
from summ import timeline
def cells(col, row):
    if col in (6, 7) and row in (5, 6, 7):
        return (0, 0, 0, 8)         # ground property 8 => cell flags 0x800 => slope 4 (deep water) under the landing zone
    return (0, 0, 0, 0)
res = P.run(dict(item=16, n=40, holds={1: W.SQUARE}, after=0, cells=cells))
print(timeline(res, 1, cols=['status', 'anim_t', 'px', 'pz', 'ground_144', 'slope_18C', 'reset_AC', 'flags6C', 'hitc']))
for r in res['rows']:
    ev = [j for j in r['journal'] if not j.startswith('stub')]
    if 20 <= r['tick'] <= 30 and ev: print(r['tick'], ev)
