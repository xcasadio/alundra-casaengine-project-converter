"""q8_p1.py - P1 (Q-G2C-9): tick-by-tick ForceAdjusted, position, direction, lock and journal of an object thrown east into a wall of column 5 (x 120..143, walkability 0x40),
hero at (100,100) walking to 109, Square at T=61. REAL code (c4_scen harness). Values for the DLL probe expectations.
Run: python -B -I -X utf8 q8_p1.py"""
import os, sys
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import c4_scen as S
import gl

wall = lambda cc, rr: dict(wk=0x40) if cc == 5 else None
for name in ('tonneau34', 'tronc117', 'etoile107'):
    for hold, label in ((gl.RIGHT, 'moving (Right held at T)'), (0, 'still')):
        m, c, rows = S.throw_run(name, cells=wall, throw_hold=hold, n=75)
        print('== %s %s, wall column 5' % (name, label))
        for t in range(59, 68):
            d = rows[t - 1]['c'][c]
            j = [x for x in rows[t - 1]['journal'] if not x.startswith('stub')]
            print('   t%03d (T%+d) x=%.2f y=%.1f z=%.2f adj(+0x13C)=%d ground=%d anim_t=%d dir_t=%d lock(+0x278)=%d t24=%d carrier28=%X journal=%s' % (
                t, t - 61, d['px'] / 65536, d['py'] / 65536, d['pz'] / 65536, d['adj'], d['ground'], d['anim_t'], d['dir_t'], d['bounced'], d['t24'], d['carrier28'], j))
