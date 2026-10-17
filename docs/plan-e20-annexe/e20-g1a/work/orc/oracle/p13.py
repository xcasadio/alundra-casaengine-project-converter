import sys, os
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import pworld as P
import world as W
from summ import timeline
for hb in (1, 2):
    def cells(col, row, hb=hb):
        if col == 15:
            return (hb, 0, 0, 0)
        return (0, 0, 0, 0)
    res = P.run(dict(item=5, n=60, holds={1: W.SQUARE}, after=0, cells=cells))
    print('=== tile height byte', hb, '(= %d px)' % (hb * 16))
    print(timeline(res, 1, cols=['status', 'anim_t', 'px', 'pz', 'adj_13C', 'coll_140' if False else 'hitc', 'flags6C'], maxrows=12))
