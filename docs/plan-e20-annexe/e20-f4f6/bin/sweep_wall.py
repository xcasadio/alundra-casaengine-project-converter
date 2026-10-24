"""Sweep the hero start x (integers 96..107) and report the tick (relative to S) of ForceAdjusted against a wall column, and the x at that tick.
arrow: Hunter bow, wall col 15 (x>=360).  fire ball: wall col 6 (x>=144)."""
import sys, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import fw, world as W
SQ = W.SQUARE

def one(item, x0, col, n):
    res = fw.run(dict(item=item, n=n, holds={1: SQ}, after=0, hero_px=(x0, 100), direction=24, cells=fw.wall_col(col)))
    S = adj = None
    xs = None
    for r in res['rows']:
        for s, d in r['slots'].items():
            if s == 0:
                continue
            if S is None:
                S = r['tick']
            if adj is None and d['adj_13C']:
                adj = r['tick']
                xs = d['px'] / 65536
    return S, adj, xs

out = []
for name, item, col, n in (('arrow3 (Hunter) wall col 15', 5, 15, 70), ('fire ball (11) wall col 6', 16, 6, 40), ('ice ball (14) wall col 6', 14, 6, 40)):
    out.append('== ' + name)
    for x0 in range(96, 108):
        S, adj, xs = one(item, x0, col, n)
        out.append('  hero x0=%d : S=%s ForceAdjusted at S+%s  (x then = %.3f)' % (x0, S, None if adj is None else adj - S, xs if xs is not None else -1))
open(os.path.join('out', 'sweep_wall.txt'), 'w').write('\n'.join(out) + '\n')
print('\n'.join(out))
