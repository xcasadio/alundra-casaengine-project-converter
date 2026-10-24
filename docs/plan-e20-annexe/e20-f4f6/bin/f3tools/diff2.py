import sys, os, json
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import pworld as P
import world as W
import interp2
from p3 import wall_col
import c1world as C

scen = {
  'hunter_wall': dict(item=5, n=70, holds={1: W.SQUARE}, after=0, cells=wall_col(15)),
  'fire_rod': dict(item=16, n=70, holds={1: W.SQUARE}, after=0),
  'ice_rod_target': dict(item=14, n=64, holds={1: W.SQUARE}, after=0, targets=[(12, 12, 0)]),
  'steel_charged': dict(item=2, n=150, holds={t: W.SQUARE for t in range(1, 96)}, after=0),
  'william_charged': dict(item=6, n=130, holds={t: W.SQUARE for t in range(1, 96)}, after=0),
}
C.map14().record['Header']['Ubuff'][13] = 32
ok = True
for name, sp in scen.items():
    a = P.run(dict(sp))
    b = P.run(dict(sp, ticker=interp2.tick2))
    same = True
    for ra, rb in zip(a['rows'], b['rows']):
        if ra['slots'] != rb['slots'] or ra['journal'] != rb['journal'] or ra['rng'] != rb['rng']:
            print(name, 'DIFF at tick', ra['tick'])
            same = False
            break
    print(name, 'two interpreters identical over %d ticks:' % len(a['rows']), same)
    ok = ok and same
sys.exit(0 if ok else 1)
