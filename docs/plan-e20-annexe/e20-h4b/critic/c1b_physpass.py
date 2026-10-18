"""c1b_physpass.py - critic's own model of the force pass 0x80036828 (+0x80036614 target recompute, +0x800367E4 IncrementForce, +0x800366FC push),
run against the real code in cmips.  Compares EVERY byte written by the real code against the model's writes (no field whitelist).
Seed 4242.  Mutants: push added after the clamp / push not consumed for hero / push consumed for carriers."""
import random, sys
from cmips import *

LIST, LCOUNT = 0x80127B28, 0x80127D28
HERO = 0x80127D30
STRIDE = 0x294
MAPPTR = 0x800E4334
ICEWATER = 0x80127000
DX32, DY32 = 0x80023654, 0x80023694
TABX, TABY = 0x80028B94, 0x80028BD4


class W:
    """tiny word memory with a write set for the model"""
    def __init__(self, ram):
        self.ram = dict(ram)
        self.w = {}

    def g(self, a, n=4, signed=False):
        v = 0
        for i in range(n):
            x = self.w.get(a + i, self.ram.get(a + i, None))
            if x is None:
                if BASE <= a + i < IMG_END:
                    x = IMG[a + i - BASE + TEXT_OFF]
                else:
                    raise Incomplete("model read of absent %#x" % (a + i))
            v |= x << (8 * i)
        if signed:
            v = v - (1 << (8 * n)) if v & (1 << (8 * n - 1)) else v
        return v

    def p(self, a, v, n=4):
        for i in range(n):
            self.w[a + i] = (v >> (8 * i)) & 0xFF


def inc(cur, tgt, step):
    cur, tgt, step = s32(cur), s32(tgt), s32(step)
    if cur == tgt:
        return cur & M
    if cur < tgt:
        n = s32(cur + step)
        return (tgt if tgt < n else n) & M
    n = s32(cur - step)
    return (tgt if n < tgt else n) & M


def recompute(W_, e):
    d = W_.g(e + 0x9C)
    sp = W_.g(d + 8, 2)
    f4 = W_.g(e + 0xF4)
    dirn = s32(W_.g(e + 0x8C))
    if f4 == sp and W_.g(e + 0x94) == (dirn & M):
        a2 = W_.g(d + 0xD, 1) & 0xF
        if W_.g(e + 0xF0) == a2:
            return
    else:
        W_.p(e + 0xF4, sp)
        W_.p(e + 0xBC, (W_.g(DX32 + 2 * dirn, 2, True) * sp) & M)
        W_.p(e + 0xC0, (W_.g(DY32 + 2 * dirn, 2, True) * sp) & M)
        W_.p(e + 0x94, dirn & M)
        a2 = W_.g(d + 0xD, 1) & 0xF
    W_.p(e + 0xF0, a2)
    dx = s32(W_.g(e + 0xBC) - W_.g(e + 0xC4))
    dx = s32(-dx) if dx < 0 else dx
    W_.p(e + 0xD4, (dx >> a2) & M)
    dy = s32(W_.g(e + 0xC0) - W_.g(e + 0xC8))
    dy = s32(-dy) if dy < 0 else dy
    W_.p(e + 0xD8, (dy >> a2) & M)


def pushfn(W_, e, mut=None):
    i = W_.g(e + 0x188) & 0xF
    tx = W_.g(TABX + 4 * i, 4, True)
    ty = W_.g(TABY + 4 * i, 4, True)
    sh = W_.g(W_.g(MAPPTR) + 8, 1) & 31
    px, py = s32(W_.g(e + 0xCC)), s32(W_.g(e + 0xD0))
    if mut == "nopush":
        px = py = 0
    W_.p(e + 0xCC, 0); W_.p(e + 0xD0, 0)
    if mut == "push_shifted":
        ax = (s32(W_.g(e + 0xC4) + tx) + px) >> sh
        ay = (s32(W_.g(e + 0xC8) + ty) + py) >> sh
    else:
        ax = (s32(W_.g(e + 0xC4) + tx) >> sh) + px
        ay = (s32(W_.g(e + 0xC8) + ty) >> sh) + py
    ax, ay = s32(ax), s32(ay)
    posx, posy = s32(W_.g(e + 0x114)), s32(W_.g(e + 0x118))
    if s32(posx + ax) < s32(W_.g(e + 0x108)):
        ax = s32(W_.g(e + 0x108)) - posx; W_.p(e + 0x13C, 1)
    elif s32(W_.g(e + 0xFC)) < s32(posx + ax):
        ax = s32(W_.g(e + 0xFC)) - posx; W_.p(e + 0x13C, 1)
    if s32(posy + ay) < s32(W_.g(e + 0x10C)):
        ay = s32(W_.g(e + 0x10C)) - posy; W_.p(e + 0x13C, 1)
    elif s32(W_.g(e + 0x100)) < s32(posy + ay):
        ay = s32(W_.g(e + 0x100)) - posy; W_.p(e + 0x13C, 1)
    W_.p(e + 0xDC, ax & M); W_.p(e + 0xE0, ay & M)


def gravity_clamp(W_, e):
    mp = W_.g(MAPPTR)
    v1 = s32(W_.g(e + 0xB8) - (W_.g(mp + 4, 2) << 8))
    a1 = W_.g(mp + 6, 2) << 8
    if a1 < abs(v1):
        v1 = a1 if v1 > 0 else -a1
    W_.p(e + 0xB8, v1 & M)


def model_pass(W_, mut=None):
    n = s32(W_.g(LCOUNT))
    for i in range(max(n, 0)):
        e = W_.g(LIST + 4 * i)
        if e == HERO:
            f8 = s32(W_.g(e + 0xF8))
            if f8 != 0:
                if (W_.g(e + 0x6C) & 0x100) and (W_.g(e + 0x180) & 0x10) and not (s32(W_.g(ICEWATER)) > 0):
                    W_.p(e + 0xB8, (f8 * 160) & M)
                else:
                    W_.p(e + 0xB8, (f8 << 8) & M)
            else:
                if W_.g(e + 0x6C) & 0x100:
                    gravity_clamp(W_, e)
            recompute(W_, e)
            t0, s3 = W_.g(e + 0xD4), W_.g(e + 0xD8)
            if W_.g(e + 0x180) & 0x20:
                t0 = ((W_.g(e + 0xD4) * 0x1000) >> 16) & M
                s3 = ((W_.g(e + 0xD8) * 0x1000) >> 16) & M
            a3, s1 = W_.g(e + 0xBC), W_.g(e + 0xC0)
            if (W_.g(e + 0x180) & 8) and not (s32(W_.g(ICEWATER)) > 0):
                a3 = ((s32(W_.g(e + 0xBC)) * 0x8000) >> 16) & M
                s1 = ((s32(W_.g(e + 0xC0)) * 0x8000) >> 16) & M
            W_.p(e + 0xC4, inc(W_.g(e + 0xC4), a3, t0))
            W_.p(e + 0xC8, inc(W_.g(e + 0xC8), s1, s3))
            pushfn(W_, e, "nopush" if mut == "hero_nopush" else mut)
            W_.p(e + 0xE4, W_.g(e + 0xDC)); W_.p(e + 0xE8, W_.g(e + 0xE0)); W_.p(e + 0xEC, W_.g(e + 0xB8))
            continue
        if W_.g(e + 0x28) != 0 and mut != "carrier_consumes":
            for off in (0xB8, 0xC8, 0xC4, 0xE0, 0xDC, 0xEC, 0xE8, 0xE4):
                W_.p(e + off, 0)
            continue
        f8 = s32(W_.g(e + 0xF8))
        if f8 != 0:
            if (W_.g(e + 0xF8, 2) == 0x8000) and not (W_.g(e + 0x6C) & 0x100):
                W_.p(e + 0xB8, 0)
            else:
                W_.p(e + 0xB8, (f8 << 8) & M)
        else:
            if W_.g(e + 0x6C) & 0x100:
                gravity_clamp(W_, e)
        recompute(W_, e)
        W_.p(e + 0xC4, inc(W_.g(e + 0xC4), W_.g(e + 0xBC), W_.g(e + 0xD4)))
        W_.p(e + 0xC8, inc(W_.g(e + 0xC8), W_.g(e + 0xC0), W_.g(e + 0xD8)))
        pushfn(W_, e, mut if mut != "carrier_consumes" else None)
        W_.p(e + 0xE4, W_.g(e + 0xDC)); W_.p(e + 0xE8, W_.g(e + 0xE0)); W_.p(e + 0xEC, W_.g(e + 0xB8))


def rw(R, small=False):
    k = R.random()
    if k < 0.05:
        return R.choice([0x7FFFFFFF, 0x80000000, 0x7FFFF000, 0xFFFFFFFF, 0x80010000]) & M
    if k < 0.4:
        return R.randint(-3000, 3000) & M
    if k < 0.8:
        return R.randint(-(1 << 20), 1 << 20) & M
    return R.randint(-(1 << 26), 1 << 26) & M


def build(R, with_hero):
    ram = {}

    def p(a, v, n=4):
        for i in range(n):
            ram[a + i] = (v >> (8 * i)) & 0xFF
    mp = 0x80200000
    p(MAPPTR, mp)
    p(mp + 4, R.choice([0, 3, 10, 40]), 2)
    p(mp + 6, R.choice([5, 60, 200, 1000]), 2)
    p(mp + 8, R.choice([0, 0, 1, 2, 33]), 1)
    p(ICEWATER, R.choice([0, 1, (-1) & M]))
    nent = R.randint(1, 6)
    slots = R.sample(range(0, 64), nent)
    if with_hero:
        slots = [0] + [s for s in slots if s != 0][:nent - 1]
    else:
        slots = [s for s in slots if s != 0] or [5]
    p(LCOUNT, len(slots))
    defs = 0x80210000
    for k, s in enumerate(slots):
        e = HERO + STRIDE * s
        p(LIST + 4 * k, e)
        d = defs + 0x20 * k
        p(e + 0x9C, d)
        p(d + 8, R.choice([0, 1, 100, 300, 0x1000]), 2)
        p(d + 0xD, R.getrandbits(8), 1)
        p(e + 0x28, R.choice([0, 0, 0, 0x80210000 + 4 * k]))
        p(e + 0x6C, R.getrandbits(32) & (0x100 | 0xFFFF0000 | R.getrandbits(16)))
        p(e + 0x8C, R.randint(0, 31)); p(e + 0x94, R.randint(0, 31))
        p(e + 0xF4, R.choice([0, 100, 300])); p(e + 0xF0, R.randint(0, 15))
        p(e + 0xF8, R.choice([0, 0, 1, 3, 0x8000, 0x18000, (-5) & M]))
        for off in (0xB8, 0xBC, 0xC0, 0xC4, 0xC8, 0xCC, 0xD0, 0xD4, 0xD8):
            p(e + off, rw(R))
        for off in (0xDC, 0xE0, 0xE4, 0xE8, 0xEC, 0x13C):
            p(e + off, R.getrandbits(32) if off != 0x13C else R.choice([0, 1]))
        p(e + 0x180, R.getrandbits(8)); p(e + 0x188, R.getrandbits(8))
        p(e + 0x114, R.randint(0, 0x4E00000)); p(e + 0x118, R.randint(0, 0x3C00000))
        p(e + 0x108, R.randint(-0x100000, 0x200000) & M); p(e + 0xFC, R.randint(0x4000000, 0x4E00000))
        p(e + 0x10C, R.randint(-0x100000, 0x200000) & M); p(e + 0x100, R.randint(0x3000000, 0x3C00000))
    return ram


def stubs_none():
    return {}


def go(R, with_hero, mut=None):
    ram = build(R, with_hero)
    m = Mem(ram, strict=True)
    c = Cpu(m)
    c.run(0x80036828, ())
    # model
    Wm = W(ram)
    model_pass(Wm, mut)
    real = {}
    for a, n, v in m.wlog:
        for i in range(n):
            real[a + i] = (v >> (8 * i)) & 0xFF
    # drop the callee stack frame
    real = {a: v for a, v in real.items() if not (0x801FFE00 <= a < 0x80200000)}
    return real, Wm.w, c, ram


if __name__ == "__main__":
    R = random.Random(4242)
    N = 800
    for mut in (None, "push_shifted", "hero_nopush", "carrier_consumes", "nopush"):
        R = random.Random(4242)
        diff = 0; heroes = 0; cov = set()
        for n in range(N):
            hh = (n % 2 == 0)
            real, mod, c, ram = go(R, hh, mut)
            cov |= c.visited
            if real != mod:
                diff += 1
                if mut is None and diff < 5:
                    ks = sorted(set(real) | set(mod))
                    bad = [(hex(k), real.get(k), mod.get(k)) for k in ks if real.get(k) != mod.get(k)]
                    print("DIFF", n, bad[:6])
        print("mutant", mut, "vectors differing from the real code:", diff, "of", N)
    # coverage of the pass (function range)
    inpass = [a for a in range(0x80036828, 0x80036BE0, 4)]
    print("coverage of 0x80036828..0x80036BE0:", len([a for a in inpass if a in cov]), "/", len(inpass))
    print("uncovered:", [hex(a) for a in inpass if a not in cov][:60])
