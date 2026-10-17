import sys, os, json
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import pworld as P
import world as W
from summ import timeline, traj

def wall_col(c, wk=0x40, hb=0):
    def cells(col, row):
        if col == c:
            return (hb, 0, wk, 0)
        return (0, 0, 0, 0)
    return cells

if __name__ == '__main__':
    # S2: wall at col 15 (x 360..383)
    spec = dict(item=5, n=60, holds={1: W.SQUARE}, after=0, hero_px=(100, 100), cells=wall_col(15))
    res = P.run(spec)
    print(timeline(res, 1, every=0))
    print(traj(res, 1, 55, 60))
    print('---- trajectory around impact')
    for r in res['rows']:
        d = r['slots'].get(1)
        if d and 40 <= r['tick'] <= 60:
            pass
    print(traj(res, 1, 40, 52))
