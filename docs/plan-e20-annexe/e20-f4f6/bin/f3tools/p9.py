import sys, os
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import pworld as P
import world as W
from summ import traj
for d in (0, 8, 16, 24):
    res = P.run(dict(item=5, n=14, holds={1: W.SQUARE}, after=0, direction=d, hero_px=(300, 300)))
    r10 = res['rows'][9]
    h = r10['slots'][0]
    a = r10['slots'][1]
    print('hero dir', d, 'hero adir', h['adir'], 'hero anim', h['anim_c'], '| arrow spawn: dir_t', a['dir_t'], 'adir', a['adir'], 'pos', a['px']/65536, a['py']/65536, a['pz']/65536)
    print(traj(res, 1, 11, 13, cols=('px', 'py', 'fx', 'fy', 'adir')))
