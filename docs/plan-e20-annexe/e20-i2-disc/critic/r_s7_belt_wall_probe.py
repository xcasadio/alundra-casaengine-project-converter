"""s7_belt_wall_probe.py - READ-ONLY probe: does the binary slide an idle hero pushed by a belt along a wall corner (the mover is force based, not facing based)?
Same harness copy, same rules: every number read from the emulated binary."""
import os, sys
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, r'C:\Users\casad\AppData\Roaming\Python\Python314\site-packages')
sys.path.insert(0, os.path.join(HERE, 'oc', 'base'))
sys.path.insert(0, HERE)
import rworld as R
from s5_belt_probe import probe, show

if __name__ == '__main__':
    # belt south (dir 1: 0x40 | 0x10 | 0x04 = 0x54) on columns 10..40; a wall row at row 8 (y 128..143) for columns 0..20 only (x < 504); free for columns >= 21.
    lay = R.Layout().rect(10, 5, 40, 40, hb=0, sl=0x54).rect(0, 8, 20, 8, wk=0x40)
    res, rec = probe('S1', lay, (500, 100), 80, [(1, 0)])
    show('S1 idle hero (facing east, direction 24) pushed SOUTH by a belt against a wall that covers the left half of his box', rec, [1, 10, 20, 30, 35, 40, 45, 50, 60, 70, 80])
    # the same with the hero facing south (direction 0)
    lay2 = R.Layout().rect(10, 5, 40, 40, hb=0, sl=0x54).rect(0, 8, 20, 8, wk=0x40)
    res2, rec2 = probe('S2', lay2, (500, 100), 80, [(1, 0)])
    # direction is a spec key of setup_r: re-run with direction=0
    spec = dict(name='S2', desc='S2', hero_px=(500, 100), n=81, pad=[(1, 0)], layout=lay2, inv='kit', hero_anim=0, direction=0)
    rec3 = {}

    def ev(t):
        def fn(m, slots):
            rec3[t - 1] = dict(px=R.s32(m.peek(R.HERO + 0x114)), py=R.s32(m.peek(R.HERO + 0x118)), adj=R.s32(m.peek(R.HERO + 0x13C)))
        return fn
    spec['events'] = {t: ev(t) for t in range(2, 82)}
    R.run_r(spec)
    print('== S2 same, hero facing south (direction 0): pos x/y and ForceAdjusted')
    for t in [1, 10, 20, 30, 35, 40, 45, 50, 60, 70, 80]:
        r = rec3.get(t)
        if r:
            print('%4d  (%8.3f,%8.3f) adj=%d' % (t, r['px'] / 65536.0, r['py'] / 65536.0, r['adj']))
