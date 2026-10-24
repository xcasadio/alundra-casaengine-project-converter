import os, sys
sys.path.insert(0, os.getcwd())
import c4_scen as S
import gl
wall = lambda cc, rr: dict(wk=0x40) if cc == 6 else None
for name in ('tonneau34', 'tronc117', 'etoile107'):
    m, c, rows = S.throw_run(name, cells=wall, throw_hold=gl.RIGHT, n=95)
    print('==', name, 'moving wall column 6')
    for t in range(61, 86):
        d = rows[t - 1]['c'][c]
        print('   T%+d x=%.2f z=%.2f adj=%d ground=%d anim_t=%d dir_t=%d lock=%d status=%d' % (t - 61, d['px'] / 65536, d['pz'] / 65536, d['adj'], d['ground'], d['anim_t'], d['dir_t'], d['bounced'], d['status']))
