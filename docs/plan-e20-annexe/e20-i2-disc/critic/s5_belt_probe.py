"""s5_belt_probe.py - READ-ONLY probe of the belts (+0x188), the lock bit and the SlideEffectId shift, on the real code of ALUN_CD.EXE (France) in the MIPS emulator of the annex
docs/plan-e20-annexe/e20-d/oracle (replayed from a COPY in ./oc/base; the repository is not touched).  Nothing here knows an expected value: every number is read from the emulated binary.
usage: python -B s5_belt_probe.py"""
import os, sys
HERE = os.path.dirname(os.path.abspath(__file__))
BASE = os.path.join(HERE, 'oc', 'base')
sys.path.insert(0, r'C:\Users\casad\AppData\Roaming\Python\Python314\site-packages')   # capstone (user site is not on the path under python -I)
sys.path.insert(0, BASE)
import rworld as R
import rscen as RS

HERO = R.HERO
UP, RIGHT, DOWN, LEFT = R.UP, R.RIGHT, R.DOWN, R.LEFT


def s32(v):
    v &= 0xFFFFFFFF
    return v - (1 << 32) if v & 0x80000000 else v


def probe(name, layout, hero_px, n, pad, shift=0, inv='kit', hero_anim=0, gravity=128, visc=4096):
    rec = {}

    def ev(t):
        def fn(m, slots):
            rec[t - 1] = dict(attr=m.peek(HERO + 0x188) & 0xFFFFFFFF, adjx=s32(m.peek(HERO + 0xDC)), adjy=s32(m.peek(HERO + 0xE0)),
                              fx=s32(m.peek(HERO + 0xC4)), fy=s32(m.peek(HERO + 0xC8)), px=s32(m.peek(HERO + 0x114)), py=s32(m.peek(HERO + 0x118)),
                              pz=s32(m.peek(HERO + 0x11C)), anim=m.peek(HERO + 0x88) & 0xFF, cur=m.peek(HERO + 0x90) & 0xFF, slope=s32(m.peek(HERO + 0x18C)),
                              finx=s32(m.peek(HERO + 0xE4)), adj=s32(m.peek(HERO + 0x13C)), pushx=s32(m.peek(HERO + 0xCC)), pushy=s32(m.peek(HERO + 0xD0)))
        return fn

    def setup(m):
        m.poke8(0x801D0000 + 8, shift & 0xFF)        # SlideEffectId in the map header block of the harness (MAPHDR + 8)

    spec = dict(name=name, desc=name, hero_px=hero_px, n=n + 1, pad=pad, layout=layout, inv=inv, hero_anim=hero_anim,
                events={t: ev(t) for t in range(2, n + 2)}, setup=setup)
    res = R.run_r(spec)
    return res, rec


def show(title, rec, ticks):
    print('== ' + title)
    print('  t   attr188   adj(DC,E0)          F(C4,C8)            pos(px)             anim slope')
    for t in ticks:
        r = rec.get(t)
        if r is None:
            continue
        print('%4d  %08x  (%8d,%8d)  (%8d,%8d)  (%8.3f,%8.3f)  %02x>%02x  %d adjflag=%d' % (
            t, r['attr'], r['adjx'], r['adjy'], r['fx'], r['fy'], r['px'] / 65536.0, r['py'] / 65536.0, r['anim'], r['cur'], r['slope'], r['adj']))


def belt(sl, hb=0, wk=0, gp=0):
    return R.Layout().rect(10, 5, 40, 40, hb=hb, sl=sl, wk=wk, gp=gp)


if __name__ == '__main__':
    # B1: dry belt, east (dir 3), gate 0x04 (level matched), no lock: Slope byte 0x40 | 0x30 | 0x04 = 0x74.  Hero idle at (500, 200) on a flat cell of height 0.
    for label, sl in (('B1 east gate0x04 nolock (0x74)', 0x74), ('B2 east gate0x08 only (0x78)', 0x78), ('B2b east gate0x0C (0x7C)', 0x7C), ('B3 east lock gate0x04 (0xF4)', 0xF4),
                      ('B3b east gate 0 (0x70, no gate bit)', 0x70)):
        res, rec = probe(label, belt(sl), (500, 200), 12, [(1, 0)])
        show(label + '  [hero idle, SlideEffectId 0]', rec, range(0, 9))
    # B4: all four directions, one line each (attr word + adj at tick 6)
    print('== B4 the four directions (idle, 0x04 gate, slope byte 0x44 | dir<<4)')
    for d in range(4):
        sl = 0x44 | (d << 4)
        res, rec = probe('B4-d%d' % d, belt(sl), (500, 200), 8, [(1, 0)])
        r = rec[6]
        print('  dir %d slope byte %02x: attr %08x adj (%d,%d) pos %.3f,%.3f' % (d, sl, r['attr'], r['adjx'], r['adjy'], r['px'] / 65536.0, r['py'] / 65536.0))
    # B5: SlideEffectId = 1 (shift): idle on the east belt and walking Right on flat ground
    res, rec = probe('B5 east belt shift1', belt(0x74), (500, 200), 12, [(1, 0)], shift=1)
    show('B5 east belt, hero idle, SlideEffectId 1', rec, range(0, 9))
    res, rec = probe('B5b flat walk R shift0', R.Layout().rect(10, 5, 40, 40, hb=0), (500, 200), 25, [(1, RIGHT)], shift=0)
    show('B5b flat, walking Right, SlideEffectId 0', rec, [5, 10, 15, 20])
    res, rec = probe('B5c flat walk R shift1', R.Layout().rect(10, 5, 40, 40, hb=0), (500, 200), 25, [(1, RIGHT)], shift=1)
    show('B5c flat, walking Right, SlideEffectId 1', rec, [5, 10, 15, 20])
    # B6: walking Right on the east belt (adds) and Left against it
    res, rec = probe('B6 walk R on east belt', belt(0x74), (500, 200), 25, [(1, RIGHT)])
    show('B6 walking Right on the east belt', rec, [3, 5, 10, 15, 20])
    res, rec = probe('B6b walk L on east belt', belt(0x74), (500, 200), 25, [(1, LEFT)])
    show('B6b walking Left against the east belt', rec, [3, 5, 10, 15, 20])
    # B7: lock bit with the pad held
    res, rec = probe('B7 lock + Right held', belt(0xF4), (500, 200), 25, [(1, RIGHT)])
    show('B7 lock cell (0xF4), Right held', rec, [1, 2, 3, 5, 10, 15, 20])
