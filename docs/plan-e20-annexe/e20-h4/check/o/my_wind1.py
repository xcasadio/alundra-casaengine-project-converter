"""my_wind1.py - checker's OWN model of the wind 1 pilot 0x80035EB0 (all phases) from my reading of the disassembly, vs the REAL code,
random worlds.  Stubs (journalled): SpawnWarpEntity 0x80039F58 (returns a fake entity), PlaySound 0x800490FC, CreateEffect 0x8003BDD8.
Everything else (the driver, the guard 0x800351B8, the cancel 0x800350C0, the query 0x8003AF70 + sort, DestroyNoFx 0x8003A774) is REAL.
Entry point: the DRIVER 0x8003634C with [0x80127164] = 0x31.
usage: python -B my_wind1.py [N] [seed]"""
import random
import sys
import spellworld as S
from machine import s32, M32
import my_query as Q

HERO = 0x80127D30
STRIDE = 0x294
INUSE, COUNTER, PHASE = 0x80127164, 0x8012715C, 0x80127160
LAST = 0x8012712C
RNG = 0x80098708
POOL = 0x801EC000
SHEET = Q.SHEET
TABLE = [30, 40, 50, 60, 70, 80, 90, 100]


def u32(x):
    return x & M32


def lcg(s):
    return u32(s * 0x7D2B89DD + 0xE06A02E7)


class Ctx:
    def __init__(self):
        self.j = []
        self.pool = []


def install(m, ctx):
    def spawn(mm):
        sp = mm.r[29]
        a = [mm.r[4] & M32, mm.r[5] & M32, mm.r[6] & M32, mm.r[7] & M32] + [mm.peek(sp + 0x10 + 4 * i) & M32 for i in range(3)]
        ctx.j.append(('spawn',) + tuple(a))
        mm.r[2] = ctx.pool.pop(0) if ctx.pool else 0

    def snd(mm):
        ctx.j.append(('sound', mm.r[4] & M32))
        mm.r[2] = 0

    def fx(mm):
        sp = mm.r[29]
        ctx.j.append(('fx', mm.r[4] & M32, mm.r[5] & M32, mm.r[6] & M32, mm.r[7] & M32, mm.peek(sp + 0x10) & M32, mm.peek(sp + 0x14) & M32))
        mm.r[2] = 0
    m.hooks[0x80039F58] = spawn
    m.hooks[0x800490FC] = snd
    m.hooks[0x8003BDD8] = fx


def model(m, st, ents_snapshot):
    """returns (ret, journal, rng, last, phase, anim, spawned_fields) following my reading of 0x80035EB0 (+ the driver 0x8003634C)"""
    j = []
    rng = st['rng']
    last = st['last']
    phase = st['phase']
    counter = st['counter']
    anim = st['anim']
    hero_slope = st['slope']
    pool = list(st['pool'])
    fields = {}                                   # pointer -> dict of written fields

    def guard():
        # 0x800351B8: slope 4 -> cancel (the cancel of wind 1 (0x31) is neutral, [0x80127164] = 0) and 1 ; anim not in {0x32,0x33} -> same ; else 0
        if hero_slope == 4 or (u32(anim - 0x32) >= 2):
            return 1
        return 0

    def driver_return(res):
        return res

    # --- pilot 0x80035EB0
    ret = None
    if phase == 1 or phase == 0:
        if phase == 0:
            last = 0
            phase = 1
        if guard():
            ret = 1
        elif anim != 0x33:
            ret = 0
        else:
            # table scan
            tk = TABLE + [0xFFFF]
            idx = 0
            if counter < tk[0]:
                pass                                   # s3 stays 0
            else:
                while True:
                    if tk[idx] == counter:
                        # strike
                        keys, ptrs = Q.my_query(m, HERO, 1, 0xB, st['count'])
                        sel = 0
                        for k in range(len(keys)):
                            cand = ptrs[k]
                            if u32(st['hz'] + 0x500000) < 0 :
                                pass
                            hz = st['hz']
                            cz = m.peek(cand + 0x11C)
                            # slt v0, hz + 0x500000, candZ  (signed)
                            if s32(u32(hz + 0x500000)) < s32(cz):
                                continue
                            if cand == last:
                                continue
                            if s32(keys[k]) < 0x6401:
                                sel = cand
                            break
                        if sel:
                            j.append(('spawn', HERO, 0, 0x16, m.peek(sel + 0x114), m.peek(sel + 0x118), u32(m.peek(sel + 0x11C) + 0x800000), 0))
                            p = pool.pop(0) if pool else 0
                            if p:
                                fields[p] = {'forcez': 0xFFF80000}
                            last = sel
                        else:
                            r1 = lcg(rng)
                            r2 = lcg(r1)
                            rng = r2
                            hi1 = (r1 * 0xC00001) >> 32
                            hi2 = (r2 * 0x800001) >> 32
                            x = u32(st['hx'] + hi1 + 0xFFA00000)
                            y = u32(st['hy'] + hi2 + 0xFFC00000)
                            j.append(('spawn', HERO, 0, 0x16, x, y, u32(st['hz'] + 0x800000), 0))
                            p = pool.pop(0) if pool else 0
                            if p:
                                if m.peek(p + 0x144) != 0:
                                    fields[p] = {'status': 4}
                                else:
                                    fields[p] = {'forcez': 0xFFF80000}
                            last = 0
                    idx += 1
                    if counter < tk[idx]:
                        break
            if tk[idx] == 0xFFFF and not (counter < tk[0] and idx == 0 and False):
                anim = 0x34
                phase += 1
            ret = 0
    elif phase == 2:
        ret = 1 if anim != 0x34 else 0
    else:
        ret = 0
    # --- driver epilogue
    counter_after = counter + 1 if counter <= 0x7FFFFFFE else counter
    inuse_after = 0 if ret != 0 else 0x31
    return ret, j, rng, last, phase, anim, fields, counter_after, inuse_after


def main(N, seed):
    rnd = random.Random(seed)
    m, STATS, placed = S.make(item=None, indices=(1,))
    ctx = Ctx()
    install(m, ctx)
    mism = 0
    cnts = {}
    m.cov.clear()
    for n in range(N):
        for b in range(0, 16 * 0x20):
            m.poke8(SHEET + b, 0)
        for k in range(16):
            for t in range(0, 14):
                m.poke8(SHEET + 0x20 * k + 2 + t, rnd.choice([0x10, 0x10, 0x20, 0x80, 0xC0, 0x40, 0x00, 0x90]))
        phase = rnd.choice([0, 1, 1, 1, 1, 2, 3])
        counter = rnd.choice(TABLE + TABLE + [0, 29, 31, 41, 99, 100, 101, 150, rnd.randrange(0, 200)])
        anim = rnd.choice([0x33, 0x33, 0x33, 0x32, 0x34, 0x31, 0])
        slope = rnd.choice([0, 0, 0, 1, 4])
        cnt = rnd.choice([0, 1, 2, 3, 4, 5, 8])
        hx, hy = rnd.randrange(100 << 16, 300 << 16), rnd.randrange(100 << 16, 300 << 16)
        hz = rnd.choice([0, 0x8000, 0x10000, 5 << 16])
        rng = rnd.getrandbits(32)
        last_ptr = 0
        m.poke(HERO + 0x88, anim)
        m.poke(HERO + 0x18C, slope)
        m.poke(HERO + 0x114, hx)
        m.poke(HERO + 0x118, hy)
        m.poke(HERO + 0x11C, hz)
        m.poke(COUNT := Q.COUNT, cnt)
        for i in range(1, 64):
            e = Q.ent(i)
            m.poke(e + 0x10, rnd.choice([2, 2, 2, 3, 0, 4]))
            m.poke(e + 0x20, 0 if rnd.random() < 0.95 else 7)
            m.poke(e + 0x6C, 1 if rnd.random() < 0.85 else 0)
            m.poke(e + 0x1D0, 0 if rnd.random() < 0.95 else 1)
            m.poke(e + 0x1CC, 0 if rnd.random() < 0.95 else 1)
            m.poke(e + 0xB4, 0 if rnd.random() < 0.95 else 0x40)
            m.poke(e + 0x1C4, 0 if rnd.random() < 0.05 else SHEET + 0x20 * rnd.randrange(16))
            ox = rnd.choice([0, 20, 60, 100, 150, 159, 160, 161, 170, -90, -160, 40])
            oy = rnd.choice([0, 10, 30, 0, 1, -20])
            m.poke(e + 0x114, (hx + (ox << 16)) & M32)
            m.poke(e + 0x118, (hy + (oy << 16)) & M32)
            m.poke(e + 0x11C, (hz + rnd.choice([0, 0, 0x10000, 60 << 16, 80 << 16, 81 << 16, -(5 << 16)])) & M32)
        # last target: one of the slots sometimes
        last = Q.ent(rnd.randrange(1, 8)) if rnd.random() < 0.4 else 0
        m.poke(LAST, last)
        pool = []
        for k in range(8):
            p = POOL + STRIDE * k
            for off in range(0, STRIDE, 4):
                m.poke(p + off, 0)
            m.poke(p + 0x144, rnd.choice([0, 1]))
            pool.append(p)
        ctx.pool = list(pool)
        ctx.j.clear()
        m.poke(INUSE, 0x31)
        m.poke(COUNTER, counter)
        m.poke(PHASE, phase)
        m.poke(RNG, rng)
        st = dict(rng=rng, last=last, phase=phase, counter=counter, anim=anim, slope=slope, pool=pool, count=cnt, hx=hx, hy=hy, hz=hz)
        exp = model(m, st, None)
        # snapshot the pool before (the real DestroyNoFx may write into the fake entity)
        ret = s32(m.call(0x8003634C))
        got_j = list(ctx.j)
        got = (ret, got_j, m.peek(RNG), m.peek(LAST), s32(m.peek(PHASE)), s32(m.peek(HERO + 0x88)), s32(m.peek(COUNTER)), s32(m.peek(INUSE)))
        eret, ej, erng, elast, ephase, eanim, efields, ecounter, einuse = exp
        # compare the observable
        ok = (got[0] == eret and got[2] == erng and got[3] == elast and got[4] == ephase and got[5] == eanim and got[6] == ecounter and got[7] == einuse)
        # journal: only the spawn calls (sounds none here)
        gj = [x for x in got_j if x[0] == 'spawn']
        ok = ok and gj == [tuple(x) for x in ej]
        # spawned entity fields
        for p, f in efields.items():
            if 'forcez' in f and m.peek(p + 0xB8) != f['forcez']:
                ok = False
            if 'status' in f and m.peek(p + 0x10) != f['status']:
                ok = False
        if not ok:
            mism += 1
            if mism <= 4:
                print('MISMATCH', n, 'phase', phase, 'counter', counter, 'anim', hex(anim), 'slope', slope, 'cnt', cnt)
                print('   real ', got[0], hex(got[2]), hex(got[3]), got[4], hex(got[5]), got[6], got[7], gj)
                print('   model', eret, hex(erng), hex(elast), ephase, hex(eanim), ecounter, einuse, ej)
        key = (phase, 'strike' if (counter in TABLE and anim == 0x33 and phase in (0, 1)) else 'other')
        cnts[key] = cnts.get(key, 0) + 1
    def cov(a, b):
        return len([p for p in range(a, b, 4) if p in m.cov]), (b - a) // 4
    print('vectors', N, 'seed', seed, 'mismatches', mism)
    print('categories', cnts)
    print('coverage wind1 pilot 0x80035EB0..0x80036218', cov(0x80035EB0, 0x80036218), ' driver', cov(0x8003634C, 0x800364C8))
    return mism


if __name__ == '__main__':
    N = int(sys.argv[1]) if len(sys.argv) > 1 else 1500
    seed = int(sys.argv[2]) if len(sys.argv) > 2 else 20261019
    sys.exit(1 if main(N, seed) else 0)
