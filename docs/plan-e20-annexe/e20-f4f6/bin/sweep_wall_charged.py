import sys, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import fw, world as W
H95 = {t: W.SQUARE for t in range(1, 96)}
out = ['== charged arrow 7 (William) wall col 15, hero x0 sweep; S = first tick of the type-7 entity (release R=96)']
for x0 in range(96, 108):
    res = fw.run(dict(item=6, n=165, holds=dict(H95), after=0, hero_px=(x0, 100), direction=24, cells=fw.wall_col(15)))
    S = adj = st4 = None
    xs = None
    for r in res['rows']:
        if r['tick'] < 96:
            continue
        for s, d in r['slots'].items():
            if d['type68'] != 7:
                continue
            if S is None:
                S = r['tick']
            if adj is None and d['adj_13C']:
                adj = r['tick']; xs = d['px'] / 65536
            if st4 is None and d['status'] == 4:
                st4 = r['tick']
    out.append('  hero x0=%d : S=%s ForceAdjusted at S+%s (x=%.3f), status 4 at S+%s' % (x0, S, adj - S, xs, st4 - S))
open(os.path.join('out', 'sweep_wall_charged.txt'), 'w').write('\n'.join(out) + '\n')
print('\n'.join(out))
