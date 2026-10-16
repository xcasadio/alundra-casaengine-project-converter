import sys, os, json
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import pworld as P
import world as W
from p1 import show

# S1: arrow far flight to the map edge
spec = dict(item=5, n=260, holds={1: W.SQUARE}, after=0, hero_px=(100, 100))
res = P.run(spec)
rows = res['rows']
# print only changes: the first rows, then every 20 ticks, then rows where anything other than px/cnt1c changes
prev = None
for r in rows:
    d = r['slots'].get(1)
    key = None if d is None else tuple((k, v) for k, v in d.items() if k not in ('px', 'cnt1c'))
    if r['tick'] <= 14 or key != prev or r['tick'] % 40 == 0:
        show(res, first=r['tick'], last=r['tick'], cols=['status', 'anim_t', 'anim_c', 'dir_t', 'fx', 'fy', 'fz', 'px', 'py', 'pz', 'adj_13C', 'ground_144', 'slope_18C', 'cnt1c', 'hitc', 'reset_AC', 'flags6C', 'b0'])
    prev = key
print('active slots at end', [s for s in rows[-1]['slots']])
