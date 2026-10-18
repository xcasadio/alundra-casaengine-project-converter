"""c6_fire2.py - critic's own model of the fire-2 driver 0x80035C64 (+ guard 0x800351B8, + cancel dispatcher restricted to id 0 / 0x30 -> 0x80034E08),
run against the real code in cmips with the spawn function 0x80039F58 stubbed (returns a chosen pointer).  Seed 8.  Compares EVERY byte written.
Also asserts the 'restore without guard' property: a cancel before any save copies the buffer (stale or zero) into the shake block."""
import random
from cmips import *

BLK, BUF = 0x800E4338, 0x80127138
CNT, PH, PTR, IDN, ACT = 0x8012715C, 0x80127160, 0x80127124, 0x80127128, 0x80127164
HERO = 0x80127D30
STR = 0x294
COUNT = 0x80134360


def w32(ram, a, v):
    v &= M
    for i in range(4):
        ram[a + i] = (v >> (8 * i)) & 0xFF


class S:
    def __init__(self, ram):
        self.ram = dict(ram); self.w = {}

    def g(self, a, n=4):
        v = 0
        for i in range(n):
            v |= self.w.get(a + i, self.ram.get(a + i, 0)) << (8 * i)
        return v

    def p(self, a, v, n=4):
        for i in range(n):
            self.w[a + i] = (v >> (8 * i)) & 0xFF


def restore(s):
    for i in range(9):
        s.p(BLK + 4 * i, s.g(BUF + 4 * i))


def cancel(s):
    ident = s.g(ACT)
    if ident == 0:
        return
    if ident == 0x30:
        restore(s)
        for i in range(1, s32(s.g(COUNT)) + 1):
            e = HERO + STR * i
            if s.g(e + 0x68) == 0x15 and s.g(e + 0x10) == 2:
                s.p(e + 0x10, 3); s.p(e + 0x20, 0)
    # (other ids: other cancel functions, not modelled -> the generator never uses them)
    s.p(ACT, 0)


def guard(s):
    slope = s.g(HERO + 0x18C); anim = s.g(HERO + 0x88)
    if slope == 4 or not ((anim - 0x32) & M < 2):
        cancel(s)
        return 1
    return 0


def driver(s, spawn_ret):
    ph = s.g(PH)
    if ph == 0:
        if guard(s):
            return 1
        if s.g(HERO + 0x88) != 0x33:
            return 0
        s.p(PTR, spawn_ret)
        if spawn_ret == 0:
            return 1
        for i in range(9):
            s.p(BUF + 4 * i, s.g(BLK + 4 * i))
        s.p(PH, s.g(PH) + 1)
        s.p(IDN, s.g(spawn_ret + 4))
        ph = 1
    if ph == 1:
        if guard(s):
            restore(s)
            return 1
        if s.g(s.g(PTR) + 4) == s.g(IDN):
            s.p(BLK, 1)
            c = s32(s.g(CNT))
            s.p(BLK + 8, (c >> 4) & M); s.p(BLK + 0x10, (c >> 4) & M)
            s.p(BLK + 4, (c >> 6) & M); s.p(BLK + 0xC, (c >> 6) & M)
            return 0
        restore(s)
        s.p(HERO + 0x88, 0x34)
        s.p(PH, s.g(PH) + 1)
        return 0
    if ph == 2:
        return 1 if s.g(HERO + 0x88) != 0x34 else 0
    return 0


def world(R):
    ram = {}
    ph = R.choice([0, 0, 1, 1, 2, 3])
    w32(ram, PH, ph)
    w32(ram, ACT, R.choice([0, 0x30, 0x30]))
    w32(ram, CNT, R.choice([0, 5, 100, 1000, 70000]))
    pe = 0x80220000
    w32(ram, PTR, pe)
    w32(ram, IDN, R.choice([7, 9]))
    w32(ram, pe + 4, R.choice([7, 9]))
    for i in range(9):
        w32(ram, BLK + 4 * i, R.getrandbits(32) if R.random() < 0.5 else R.choice([0, 1, 4]))
        w32(ram, BUF + 4 * i, R.getrandbits(32) if R.random() < 0.5 else R.choice([0, 2, 8]))
    w32(ram, HERO + 0x88, R.choice([0x32, 0x33, 0x33, 0x34, 0x10]))
    w32(ram, HERO + 0x18C, R.choice([0, 1, 4]))
    w32(ram, COUNT, R.choice([0, 1, 3, 6]))
    for i in range(1, 8):
        e = HERO + STR * i
        w32(ram, e + 0x68, R.choice([0x15, 0x15, 0x13]))
        w32(ram, e + 0x10, R.choice([0, 2, 3, 2]))
        w32(ram, e + 0x20, R.choice([0, 1]))
    return ram, R.choice([0, pe, pe])


if __name__ == "__main__":
    R = random.Random(8)
    N = 4000
    bad = 0
    cov = set()
    for n in range(N):
        ram, sret = world(R)
        m = Mem(ram, strict=False)
        spawned = {}

        def spawn(c, sret=sret):
            c.r[2] = sret
        c = Cpu(m, stubs={0x80039F58: spawn})
        ret = c.run(0x80035C64, ())
        cov |= c.visited
        s = S(ram)
        mret = driver(s, sret)
        real = {}
        for a, sz, v in m.wlog:
            if 0x801FFE00 <= a < 0x80200000:
                continue
            for i in range(sz):
                real[a + i] = (v >> (8 * i)) & 0xFF
        if real != s.w or (ret & M) != (mret & M):
            bad += 1
            if bad < 4:
                print("MISMATCH", n, "ret", ret, mret, {hex(k): (real.get(k), s.w.get(k)) for k in set(real) | set(s.w) if real.get(k) != s.w.get(k)})
    rng = list(range(0x80035C64, 0x80035EAC, 4))
    print("fire2 driver: %d runs, mismatches %d, coverage of the driver %d/%d" % (N, bad, len([a for a in rng if a in cov]), len(rng)))
    print("uncovered", [hex(a) for a in rng if a not in cov])
