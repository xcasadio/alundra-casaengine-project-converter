"""s9_belt_target_push_probe.py - READ-ONLY probe on the real code (harness copy): (1) a non-hero entity (the inert Beannoid of the oracle) on a belt drifts like the hero; (2) the push +0xCC/+0xD0 written
by hand on the hero and on the Beannoid is consumed (added once, not shifted, zeroed) at the next forces pass; (3) the push with SlideEffectId 1: not shifted.
Every number is read from the emulated binary; nothing here knows an expected value."""
import os, sys
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, r'C:\Users\casad\AppData\Roaming\Python\Python314\site-packages')
sys.path.insert(0, os.path.join(HERE, 'oc', 'base'))
sys.path.insert(0, HERE)
import rworld as R
W = R.W

HERO = R.HERO
s32 = R.s32


def run(name, layout, targets, n, pushes, shift=0, hero_px=(300, 100)):
    rec = {}

    def ev_snap(t):
        def fn(m, slots):
            tb = W.ent(slots[0]) if slots else None
            rec[t - 1] = dict(
                hx=s32(m.peek(HERO + 0x114)), hy=s32(m.peek(HERO + 0x118)), hadj=(s32(m.peek(HERO + 0xDC)), s32(m.peek(HERO + 0xE0))), hpush=(s32(m.peek(HERO + 0xCC)), s32(m.peek(HERO + 0xD0))),
                hattr=m.peek(HERO + 0x188) & 0xFFFFFFFF,
                tx=s32(m.peek(tb + 0x114)) if tb else None, ty=s32(m.peek(tb + 0x118)) if tb else None, tadj=(s32(m.peek(tb + 0xDC)), s32(m.peek(tb + 0xE0))) if tb else None,
                tpush=(s32(m.peek(tb + 0xCC)), s32(m.peek(tb + 0xD0))) if tb else None, tattr=(m.peek(tb + 0x188) & 0xFFFFFFFF) if tb else None, tf=(s32(m.peek(tb + 0xC4)), s32(m.peek(tb + 0xC8))) if tb else None)
            if t in pushes:
                who, px_, py_ = pushes[t]
                base = HERO if who == 'hero' else tb
                m.poke(base + 0xCC, px_ & 0xFFFFFFFF)
                m.poke(base + 0xD0, py_ & 0xFFFFFFFF)
        return fn

    def setup(m):
        m.poke8(0x801D0000 + 8, shift & 0xFF)
    spec = dict(name=name, desc=name, hero_px=hero_px, n=n + 1, pad=[(1, 0)], layout=layout, inv='kit', targets=targets, events={t: ev_snap(t) for t in range(2, n + 2)}, setup=setup)
    R.run_r(spec)
    return rec


def show(title, rec, ticks):
    print('== ' + title)
    for t in ticks:
        r = rec.get(t)
        if not r:
            continue
        print('%3d hero pos(%8.3f,%8.3f) adj%s push%s attr%08x | target pos(%s,%s) adj%s push%s attr%s F%s' % (
            t, r['hx'] / 65536.0, r['hy'] / 65536.0, r['hadj'], r['hpush'], r['hattr'],
            '%8.3f' % (r['tx'] / 65536.0) if r['tx'] is not None else '-', '%8.3f' % (r['ty'] / 65536.0) if r['ty'] is not None else '-', r['tadj'], r['tpush'],
            '%08x' % r['tattr'] if r['tattr'] is not None else '-', r['tf']))


if __name__ == '__main__':
    belt = R.Layout().rect(10, 5, 40, 40, hb=0, sl=0x74)       # east belt, gate 0x04, no lock
    flat = R.Layout().rect(10, 5, 40, 40, hb=0)
    # T1: the inert Beannoid on the east belt at tile (30, 12) (x 720..743, y 192..207), the hero far away on flat ground
    rec = run('T1', R.Layout().rect(10, 5, 40, 40, hb=0, sl=0x74), [(30, 12, 0)], 12, {})
    show('T1 the inert Beannoid (target arg (30,12) = pixels (372,104)) on an east belt covering cols 10-40, rows 5-40, hero on the same belt: does it drift?', rec, [1, 2, 3, 6, 10])
    # T2: push written by hand: hero +0xCC = 131072 (2 px) at tick 5 on flat ground; the Beannoid gets +0xD0 = -65536 at tick 5
    rec = run('T2', flat, [(30, 12, 0)], 12, {5: ('hero', 131072, 0)})
    show('T2 a push of 131072 (X) written on the hero before tick 5 (flat ground, shift 0)', rec, [3, 4, 5, 6, 7, 8])
    rec = run('T3', flat, [(30, 12, 0)], 12, {5: ('target', 0, -65536)})
    show('T3 a push of -65536 (Y) written on the Beannoid before tick 5', rec, [3, 4, 5, 6, 7, 8])
    # T4: SlideEffectId = 1: the push is NOT shifted, the belt IS
    rec = run('T4', R.Layout().rect(10, 5, 40, 40, hb=0, sl=0x74), [(30, 12, 0)], 12, {5: ('hero', 131072, 0)}, shift=1)
    show('T4 SlideEffectId 1, east belt under the hero, push 131072 written before tick 5', rec, [3, 4, 5, 6, 7])
