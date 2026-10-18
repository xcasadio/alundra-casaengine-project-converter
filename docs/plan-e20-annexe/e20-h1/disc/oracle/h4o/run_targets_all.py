import sys
import trace_targets as T
from collections import Counter
TARGETS = [(140, 100), (100, 140), (180, 100), (100, 60), (60, 100), (140, 140)]
inv = int(sys.argv[1]) if len(sys.argv) > 1 else 0
for item in range(0x2B, 0x33):
    m, res, hp0, STATS = T.run(item, TARGETS, inv=inv, hp=5000, ticks=800)
    print('item %#x inv=%d' % (item, inv))
    for p, r in res.items():
        c = Counter((h[1], h[2]) for h in r['hits'])
        first = r['hits'][0][0] if r['hits'] else None
        print('   target (%3d,%3d) moved to (%3d,%3d): hits %2d dmg %4d first tick %s by %s' % (TARGETS[list(res).index(p)][0], TARGETS[list(res).index(p)][1], r['x'], r['y'], len(r['hits']), 5000 - r['hp'], first, dict(c)))
