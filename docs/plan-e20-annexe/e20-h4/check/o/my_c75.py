"""my_c75.py - checker's OWN model of the water orb handler C75 (0x8007BD8C) from my reading of the disassembly, vs the REAL code on random states.
GetDirectionToTarget 0x8002E814 is called as the real helper inside the model.  usage: python -B my_c75.py [N] [seed]"""
import random
import sys
import spellworld as S
from machine import s32, M32
import my_query as Q

HERO = 0x80127D30
PADHOLD = 0x80126F28
RNG = 0x80098708
ATK = 0x801E8000


def u32(x):
    return x & M32


def lcg(s):
    return u32(s * 0x7D2B89DD + 0xE06A02E7)


def tab(m, base, n):
    return [m.rhs(base + 2 * i) for i in range(n)]


def main(N, seed):
    rnd = random.Random(seed)
    m, STATS, placed = S.make(item=None, indices=(1,))
    SIN = tab(m, 0x800204F0, 256)
    COS = tab(m, 0x800206F0, 256)
    TX = tab(m, 0x80023654, 32)
    TY = tab(m, 0x80023694, 32)
    me = Q.ent(3)
    mism = 0
    cat = {}
    m.cov.clear()
    for n in range(N):
        for b in range(0, 16 * 0x20):
            m.poke8(Q.SHEET + b, 0)
        for k in range(16):
            for t in range(0, 14):
                m.poke8(Q.SHEET + 0x20 * k + 2 + t, rnd.choice([0x10, 0x10, 0x20, 0x80, 0xC0, 0x00, 0x90]))
        m.poke(Q.COUNT, rnd.choice([3, 4, 6, 9]))
        hx, hy, hz = rnd.randrange(80 << 16, 300 << 16), rnd.randrange(80 << 16, 300 << 16), rnd.choice([0, 0x10000, 7 << 16])
        m.poke(HERO + 0x114, hx); m.poke(HERO + 0x118, hy); m.poke(HERO + 0x11C, hz)
        m.poke(HERO + 0xC4, rnd.choice([0, 0, 0x30000, u32(-0x20000)]))
        m.poke(HERO + 0xC8, rnd.choice([0, 0, 0x10000, u32(-0x18000)]))
        sx, sy, sz = (hx + rnd.randrange(-60 << 16, 60 << 16)) & M32, (hy + rnd.randrange(-60 << 16, 60 << 16)) & M32, rnd.choice([0, 0x20000, 0x90000])
        m.poke(me + 0x114, sx); m.poke(me + 0x118, sy); m.poke(me + 0x11C, sz)
        m.poke(me + 0x6C, rnd.choice([0x34, 0x34, 0x2134]))
        m.poke(me + 0xB8, rnd.getrandbits(32))
        m.poke(me + 0xCC, rnd.getrandbits(32)); m.poke(me + 0xD0, rnd.getrandbits(32))
        # parameters as the pilot leaves them
        eau = rnd.choice([(1024, 3072, 16, 7), (3584, 3584, 0, 8), (2048, 3072, 32, -6), (rnd.randrange(0, 4000), rnd.randrange(0, 4000), rnd.randrange(0, 70), rnd.randrange(-9, 10))])
        p0, p1, p2, p3 = eau
        m.poke(me + 0x274, u32(p0)); m.poke(me + 0x278, u32(p1)); m.poke(me + 0x27C, u32(p2)); m.poke(me + 0x280, u32(p3))
        mode = rnd.choice([0, 1])
        r = rnd.choice([0, 64, 500, 1023, 1024, 1025, 2000, 3071, 3072, 3073, 3583, 3584, 3600, rnd.randrange(0, 4200)])
        a = rnd.randrange(0, 256)
        timer = rnd.choice([5, 1, 2, 0, 0, 0, -1, -7, 300, rnd.randrange(-3, 40)])
        m.poke(me + 0x284, mode); m.poke(me + 0x288, u32(r)); m.poke(me + 0x28C, a); m.poke(me + 0x290, u32(timer))
        m.poke(me + 0x1C8, ATK)
        m.poke8(ATK, rnd.choice([0x08, 0x09, 0x89, 0x0A, 0x1B]))
        held = rnd.random() < 0.3
        m.poke16(PADHOLD, 0x20 if held else rnd.choice([0, 0x40, 0x10]))
        rng = rnd.getrandbits(32)
        m.poke(RNG, rng)
        for i in range(1, 64):
            e = Q.ent(i)
            if e == me:
                continue
            m.poke(e + 0x10, rnd.choice([2, 2, 2, 3, 0, 4]))
            m.poke(e + 0x20, 0); m.poke(e + 0x6C, 1 if rnd.random() < 0.8 else 0)
            m.poke(e + 0x1D0, 0); m.poke(e + 0x1CC, 0); m.poke(e + 0xB4, 0)
            m.poke(e + 0x1C4, 0 if rnd.random() < 0.05 else Q.SHEET + 0x20 * rnd.randrange(16))
            m.poke(e + 0x114, (sx + (rnd.choice([0, 30, 100, 119, 121, -60, 200]) << 16)) & M32)
            m.poke(e + 0x118, (sy + (rnd.choice([0, 10, 0, 40, -20]) << 16)) & M32)
        # ---- model
        exp = {}
        cnt = m.peek(Q.COUNT)
        f = lambda o: s32(m.peek(me + o))
        timer_m = timer
        if not held:
            timer_m -= 1
        exp['290'] = u32(timer_m) if not held else u32(timer)
        newrng = rng
        if timer_m > 0:
            md, rr = f(0x284), f(0x288)
            P0, P1, P2, P3 = f(0x274), f(0x278), f(0x27C), f(0x280)
            sel = P0 if md == 0 else P1
            extra = 64 if rr < P0 else 0
            if sel < rr:
                rr2 = rr - P2
                if not (sel < rr2):
                    rr2 = sel; md ^= 1
            else:
                rr2 = rr + P2 + extra
                if not (rr2 < sel):
                    rr2 = sel; md ^= 1
            ang = (f(0x28C) + P3) & 0xFF
            sx_ = SIN[ang] * rr2
            cy_ = COS[ang] * rr2
            exp['284'] = u32(md); exp['288'] = u32(rr2); exp['28C'] = ang
            exp['CC'] = u32(hx + sx_ * 3 + s32(m.peek(HERO + 0xC4)) - sx)
            exp['D0'] = u32(hy + cy_ * 2 + s32(m.peek(HERO + 0xC8)) - sy)
            exp['B8'] = u32(hz - sz + 0x80000)
            cat['orbit'] = cat.get('orbit', 0) + 1
        else:
            if timer_m == 0:
                keys, ptrs = Q.my_query(m, me, 1, m.rb(ATK) & 0xF, cnt)
                if keys and keys[0] < 0x3840:
                    t = ptrs[0]
                    d = s32(m.call(0x8002E814, u32(m.peek(t + 0x114) - sx), u32(m.peek(t + 0x118) - sy)))
                else:
                    newrng = lcg(rng)
                    d = (newrng * 0x20) >> 32
                exp['274'] = u32(TX[d] << 8); exp['278'] = u32(TY[d] << 8)
                exp['6C'] = u32(m.peek(me + 0x6C) | 0x2130)
                exp['B8'] = 0xA0000
                exp['CC'] = exp['274']; exp['D0'] = exp['278']
                cat['release'] = cat.get('release', 0) + 1
            else:
                exp['CC'] = m.peek(me + 0x274); exp['D0'] = m.peek(me + 0x278)
                cat['flight'] = cat.get('flight', 0) + 1
        m.call(0x8007BD8C, me)
        got = {'290': m.peek(me + 0x290), '284': m.peek(me + 0x284), '288': m.peek(me + 0x288), '28C': m.peek(me + 0x28C),
               'CC': m.peek(me + 0xCC), 'D0': m.peek(me + 0xD0), 'B8': m.peek(me + 0xB8), '274': m.peek(me + 0x274), '278': m.peek(me + 0x278), '6C': m.peek(me + 0x6C)}
        bad = [k for k, v in exp.items() if got[k] != v]
        if m.peek(RNG) != newrng:
            bad.append('rng')
        if bad:
            mism += 1
            if mism <= 4:
                print('MISMATCH', n, 'timer', timer, 'held', held, 'bad', bad, {k: (hex(got[k]), hex(exp[k])) for k in bad if k in exp})
    def cov(a, b):
        return len([p for p in range(a, b, 4) if p in m.cov]), (b - a) // 4
    print('vectors', N, 'seed', seed, 'mismatches', mism, cat)
    print('coverage C75 0x8007BD8C..0x8007C024', cov(0x8007BD8C, 0x8007C024))
    return mism


if __name__ == '__main__':
    N = int(sys.argv[1]) if len(sys.argv) > 1 else 2000
    seed = int(sys.argv[2]) if len(sys.argv) > 2 else 20261020
    sys.exit(1 if main(N, seed) else 0)
