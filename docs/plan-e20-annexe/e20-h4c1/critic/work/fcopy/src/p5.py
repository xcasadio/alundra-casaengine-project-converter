import sys, os, json
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import pworld as P
import world as W
import c1world as C
from summ import timeline, traj

# harness-level change, documented: the map-14 Beannoid with a 32 px tall body (header SizeZ), so that the arrow at Z+16 can touch it
C.map14().record['Header']['Ubuff'][13] = 32

spec = dict(item=5, n=45, holds={1: W.SQUARE}, after=0, hero_px=(100, 100), targets=[(24, 12, 0)])
res = P.run(spec)
print('arrow slot 2:')
print(timeline(res, 2, every=0, cols=['status', 'anim_t', 'dir_t', 'px', 'pz', 'adj_13C', 'hitc', 'reset_AC', 'flags6C', 'c1d0', 'atk_attr', 'box1d4']))
print('target slot 1:')
print(timeline(res, 1, every=0, cols=['status', 'anim_t', 'hitc', 'touch_slot', 'c1d0', 'c1cc', 'hp', 'atk_attr']))
print(traj(res, 2, 30, 42))
for r in res['rows']:
    if 36 <= r['tick'] <= 40:
        print(r['tick'], r['rng'], r['journal'])
