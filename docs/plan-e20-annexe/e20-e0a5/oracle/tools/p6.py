import sys, os, json
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import pworld as P
import world as W
import c1world as C
from summ import timeline, traj
from p3 import wall_col

def go(name, item, n, holds, direction=24, cells=None, targets=(), slot=1, every=0, first=None, last=None, cols=None, hero_px=(100, 100), tall=False):
    if tall:
        C.map14().record['Header']['Ubuff'][13] = 32
    spec = dict(item=item, n=n, holds=holds, after=0, hero_px=hero_px, direction=direction, cells=cells, targets=list(targets))
    res = P.run(spec)
    print('######', name)
    cols = cols or ['status', 'anim_t', 'anim_c', 'dir_t', 'px', 'py', 'pz', 'fz', 'adj_13C', 'ground_144', 'slope_18C', 'hitc', 'reset_AC', 'flags6C', 'atk_attr', 'atk_pow', 'box1d4']
    print(timeline(res, slot, every=every, cols=cols))
    return res

if __name__ == '__main__':
    which = sys.argv[1]
    if which == 'fire':
        res = go('fire rod ground east', 16, 90, {1: W.SQUARE})
        print(traj(res, 1, 11, 40, cols=('px', 'pz', 'fx', 'fz', 'ground_144', 'cnt1c', 'b0')))
    if which == 'ice':
        res = go('ice rod ground east', 14, 90, {1: W.SQUARE})
        print(traj(res, 1, 11, 40, cols=('px', 'pz', 'fx', 'fz', 'ground_144', 'cnt1c', 'b0')))
