"""b1b_physpass.py - brick (1), pass level: the force pass 0x80036828 over the physics list [0x80127B28 .. count 0x80127D28].
Real code run on entities in the simple case (no gravity, no Z force, UpdateEntityPhysics 0x80036614 a no-op because the cached speed/direction/acceleration
match the animation record, hero flags +0x180 = 0).  Measures: which entities consume the push (everyone in the list, the hero included), the platform
case (+0x28 != 0: forces zeroed, push left untouched), the order IncrementForce -> table/shift -> push -> map bound, FinalForce copy (+0xE4/+0xE8/+0xEC).
Own model from ../out/dis_80036828.txt; replayable vectors ../vec/vec_physpass_80036828.json.
usage: python -B b1b_physpass.py [N] [seed]"""
import json, os, random, sys
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import vtrace as V
from machine import s32, M32

OUT = os.path.join(HERE, '..', 'vec')
ENT0, STRIDE = 0x80127D30, 0x294
LIST, LCOUNT = 0x80127B28, 0x80127D28
MAPINFO_PTR, MAPINFO = 0x800E4334, 0x801D0000
RECS = 0x801D8000
TAB_X, TAB_Y = 0x80028B94, 0x80028BD4


def ent(i):
    return ENT0 + STRIDE * i


def incr(force, target, step):
    force, target, step = s32(force), s32(target), s32(step)
    if target == force:
        return force
    if force < target:
        f = s32(force + step)
        return target if target < f else f
    f = s32(force - step)
    return target if f < target else f


def apply_forces(e, sh, tx, ty, mut=None):
    idx = e[0x188] & 0xF
    ax = s32(e[0xC4] + tx[idx]) >> sh
    ay = s32(e[0xC8] + ty[idx]) >> sh
    px, py = (0, 0) if mut == 'nopush' else (s32(e[0xCC]), s32(e[0xD0]))
    ax = s32(ax + px)
    ay = s32(ay + py)
    flag = 0
    out = []
    for pos, lo, hi, a in ((e[0x114], e[0x108], e[0xFC], ax), (e[0x118], e[0x10C], e[0x100], ay)):
        n = s32(pos + a)
        if n < s32(lo):
            a = s32(lo - pos)
            flag = 1
        elif s32(hi) < n:
            a = s32(hi - pos)
            flag = 1
        out.append(a)
    return out[0], out[1], flag


def run(N, seed):
    rnd = random.Random(seed)
    m = V.TM()
    tx = [s32(m.peek(TAB_X + 4 * i)) for i in range(16)]
    ty = [s32(m.peek(TAB_Y + 4 * i)) for i in range(16)]
    m.cov.clear()
    mism, vecs = 0, []
    killed = {'nopush': 0}
    nplat = nhero = nnorm = 0
    for n in range(N):
        sh = rnd.choice([0, 0, 0, 1, 4])
        m.poke(MAPINFO_PTR, MAPINFO)
        m.poke8(MAPINFO + 8, sh)
        m.poke16(MAPINFO + 4, 128)
        m.poke16(MAPINFO + 6, 4096)
        slots = rnd.sample(range(1, 20), rnd.choice([1, 2, 3]))
        order = ([0] if rnd.random() < 0.5 else []) + slots
        rnd.shuffle(order)
        E = {}
        for k, i in enumerate(order):
            e = {}
            rec = RECS + 0x40 * k
            speed = rnd.choice([0, 256, 512])
            acc = rnd.randrange(0, 16)
            dirn = rnd.randrange(0, 32)
            m.poke16(rec + 8, speed)
            m.poke8(rec + 0xD, acc | (rnd.randrange(0, 16) << 4))
            for off in range(0, STRIDE, 4):
                m.poke(ent(i) + off, 0)
            e[0x9C] = rec
            e[0xF4] = speed
            e[0x8C] = dirn
            e[0x94] = dirn
            e[0xF0] = acc
            e[0x28] = rnd.choice([0, 0, 0, 0, 0x80128000])
            e[0xF8] = 0
            e[0x6C] = rnd.choice([0x1, 0x81, 0x5])
            if i != 0:                                  # the non-hero Z branch of the pass: gravity flag 0x100 and the Z force request +0xF8
                e[0x6C] |= rnd.choice([0, 0, 0x100])
                e[0xF8] = rnd.choice([0, 0, 0, 5, 0x8000, 0xFFFF8000, 0xFFFFFFFB])
            e[0x180] = 0
            e[0x188] = rnd.randrange(0, 1 << 8)
            e[0xBC] = rnd.choice([0, rnd.randrange(-(1 << 18), 1 << 18)]) & M32
            e[0xC0] = rnd.choice([0, rnd.randrange(-(1 << 18), 1 << 18)]) & M32
            e[0xD4] = rnd.choice([0, 0x1000, 0x10000, 0x8000, 0x40000])
            e[0xD8] = rnd.choice([0, 0x1000, 0x10000, 0x8000, 0x40000])
            e[0xC4] = rnd.choice([0, rnd.randrange(-(1 << 18), 1 << 18)]) & M32
            e[0xC8] = rnd.choice([0, rnd.randrange(-(1 << 18), 1 << 18)]) & M32
            big = [0, 0x30000, -0x30000, 0x20000, -0x20000, 196608, 131072, rnd.randrange(-1 << 22, 1 << 22)]
            e[0xCC] = rnd.choice(big) & M32
            e[0xD0] = rnd.choice(big) & M32
            e[0x114] = rnd.randrange(0, 0x4E00000)
            e[0x118] = rnd.randrange(0, 0x3C00000)
            e[0x108] = 0
            e[0xFC] = 0x4E00000 - rnd.randrange(0, 0x100000)
            e[0x10C] = 0
            e[0x100] = 0x3C00000 - rnd.randrange(0, 0x100000)
            e[0x13C] = 0
            e[0xB8] = rnd.choice([0, 0x7000, -0x9000 & M32, -0x300000 & M32, 0x20000])
            e[0xDC] = 0x55
            e[0xE0] = 0x56
            e[0xE4] = 0x57
            e[0xE8] = 0x58
            e[0xEC] = 0x59
            for off, v in e.items():
                m.poke(ent(i) + off, v & M32)
            E[i] = e
        m.poke(LCOUNT, len(order))
        for k, i in enumerate(order):
            m.poke(LIST + 4 * k, ent(i))
        vec = V.run(m, 0x80036828, ())
        ok = True
        for i in order:
            e = E[i]
            got = {off: m.peek(ent(i) + off) for off in (0xCC, 0xD0, 0xC4, 0xC8, 0xDC, 0xE0, 0xE4, 0xE8, 0xEC, 0xB8, 0x13C)}
            if e[0x28] != 0 and i != 0:                 # the hero's branch of the pass has no +0x28 test
                want = {0xCC: e[0xCC], 0xD0: e[0xD0], 0xC4: 0, 0xC8: 0, 0xDC: 0, 0xE0: 0, 0xE4: 0, 0xE8: 0, 0xEC: 0, 0xB8: 0, 0x13C: 0}
                nplat += 1
            else:
                fx = incr(e[0xC4], e[0xBC], e[0xD4])
                fy = incr(e[0xC8], e[0xC0], e[0xD8])
                e2 = dict(e)
                e2[0xC4] = fx
                e2[0xC8] = fy
                ax, ay, flag = apply_forces(e2, sh, tx, ty)
                fz = e[0xB8]
                if i != 0:
                    f8, grav = e[0xF8], bool(e[0x6C] & 0x100)
                    if f8 == 0:
                        if grav:
                            v = s32(fz - (128 << 8))
                            a = abs(v)
                            lim = 4096 << 8
                            if lim < a and not (v > 0):
                                v = -lim
                            fz = v & M32
                    elif (f8 & 0xFFFF) == 0x8000 and not grav:
                        fz = 0
                    else:
                        fz = (f8 << 8) & M32
                want = {0xCC: 0, 0xD0: 0, 0xC4: fx & M32, 0xC8: fy & M32, 0xDC: ax & M32, 0xE0: ay & M32, 0xE4: ax & M32, 0xE8: ay & M32, 0xEC: fz, 0xB8: fz, 0x13C: flag}
                if i == 0:
                    nhero += 1
                else:
                    nnorm += 1
                ax2, ay2, f2 = apply_forces(e2, sh, tx, ty, mut='nopush')
                if (ax2, ay2) != (ax, ay) or f2 != flag:
                    killed['nopush'] += 1
            if got != want:
                ok = False
                if mism < 3:
                    print('PASS MISMATCH slot', i, {hex(k): (hex(got[k]), hex(want[k])) for k in got if got[k] != want[k]})
        if not ok:
            mism += 1
        if n < 40:
            vecs.append({"id": n, "slots": order, **vec})
    cov = len([p for p in range(0x80036828, 0x80036BF8, 4) if p in m.cov])
    print('physics pass 0x80036828: vectors %d mismatches %d | entities that consumed the push: others %d, hero %d ; platform case (+0x28 != 0) %d | coverage %d/%d' % (N, mism, nnorm, nhero, nplat, cov, (0x80036BF8 - 0x80036828) // 4))
    print('  entity-results that depend on the push being added:', killed)
    bad = 0
    for v in vecs:
        ok, msg = V.replay(v)
        if not ok:
            bad += 1
            if bad <= 3:
                print('REPLAY FAIL', v['id'], msg)
    print('  replay from empty memory: %d/%d' % (len(vecs) - bad, len(vecs)))
    json.dump({"function": "0x80036828", "note": "force pass over the physics list; simple case (no gravity, no Z force, cached UpdateEntityPhysics); list = pointers at 0x80127B28, count at 0x80127D28", "vectors": vecs}, open(os.path.join(OUT, 'vec_physpass_80036828.json'), 'w'))
    return mism


if __name__ == '__main__':
    N = int(sys.argv[1]) if len(sys.argv) > 1 else 600
    seed = int(sys.argv[2]) if len(sys.argv) > 2 else 20261018
    os.makedirs(OUT, exist_ok=True)
    sys.exit(1 if run(N, seed) else 0)
