"""Quick look at the keys of a fw.run row (copy of the oracle, run from t2-proj/f4f6bin)."""
import os
import sys

HERE = os.path.join(os.path.dirname(os.path.abspath(__file__)), 'f4f6bin')
sys.path.insert(0, HERE)
import fw
import world as W

SQ = W.SQUARE
res = fw.run(dict(item=5, n=16, holds={1: SQ}, after=0, hero_px=(108, 100), direction=24, cells=fw.wall_col(5), targets=[], seed=0x12345678))
print(res.keys())
for r in res['rows'][8:14]:
    print(r['tick'], {s: {k: v for k, v in d.items() if k in ('type68', 'status', 'px', 'py', 'pz', 'adj_13C', 'fx', 'anim_t')} for s, d in r['slots'].items()})
print(sorted(res['rows'][10]['slots'][1].keys()))
