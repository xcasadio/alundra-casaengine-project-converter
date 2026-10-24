import sys, os, json
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import pworld as P
import world as W
from summ import timeline, traj

# S3: Hunter's bow arrow vs an inert Beannoid at x=300,y=104 (z 0, body z size 8) and the same target raised to z 8 (height byte 1 => 8px? use heights 0,1,2)
for h in (0, 1, 2):
    spec = dict(item=5, n=60, holds={1: W.SQUARE}, after=0, hero_px=(100, 100), targets=[(24, 12, h)])
    res = P.run(spec)
    print('==== target height', h)
    print('target slot snapshot t10:', {k: res['rows'][9]['slots'][1][k] for k in ('pz', 'terr_138', 'hp', 'status')})
    print('arrow slot 2:')
    print(timeline(res, 2, every=0))
    print('target slot 1:')
    print(timeline(res, 1, every=0, cols=['status', 'anim_t', 'hitc', 'touch_slot', 'c1d0', 'c1cc', 'hp', 'atk_attr']))
