"""c1_push.py - critic's own model of 0x800366FC (ApplyEntityForces / slide table + shift + push + clamp), run against the real code
in the critic's own MIPS interpreter (cmips). Seed 77.  Also prints the table dumps and the semantic mutants."""
import random, sys
from cmips import *

TABX, TABY = 0x80028B94, 0x80028BD4
MAPPTR = 0x800E4334


def rtab(base):
    return [s32(img_u32(base + 4 * i)) for i in range(16)]


TX, TY = rtab(TABX), rtab(TABY)


def model(e, sh, push=True, order="shift_then_push"):
    """e: dict of fields. returns (new fields dict)"""
    i = e[0x188] & 0xF
    fx, fy = s32(e[0xC4]), s32(e[0xC8])
    px, py = s32(e[0xCC]), s32(e[0xD0])
    sh &= 31
    ax = (s32(fx + TX[i]) >> sh) + px
    ay = (s32(fy + TY[i]) >> sh) + py
    ax = s32(ax); ay = s32(ay)
    flag = None
    # X clamp
    posx, posy = s32(e[0x114]), s32(e[0x118])
    if s32(posx + ax) < s32(e[0x108]):
        ax = s32(e[0x108]) - posx; flag = 1
    elif s32(e[0xFC]) < s32(posx + ax):
        ax = s32(e[0xFC]) - posx; flag = 1
    if s32(posy + ay) < s32(e[0x10C]):
        ay = s32(e[0x10C]) - posy; flag = 1
    elif s32(e[0x100]) < s32(posy + ay):
        ay = s32(e[0x100]) - posy; flag = 1
    out = {0xCC: 0, 0xD0: 0, 0xDC: ax & M, 0xE0: ay & M}
    if flag:
        out[0x13C] = 1
    return out


def rnd_word(R):
    k = R.random()
    if k < 0.3:
        return R.randint(-4096, 4096) & M
    if k < 0.6:
        return (R.randint(-(1 << 20), 1 << 20)) & M
    if k < 0.8:
        return R.randint(-(1 << 27), 1 << 27) & M
    if k < 0.9:
        return R.choice([0x7FFFFFFF, 0x80000000, 0x7FFFF000, 0x80010000, 0xFFFFFFFF, 1, 0x7FFF0000]) & M
    return R.getrandbits(32)


def gen(R):
    base = R.choice([0x80127D30, 0x80127D30 + 0x294 * 5, 0x80128000])
    e = {}
    for off in (0xC4, 0xC8, 0xCC, 0xD0, 0x188, 0x13C, 0xDC, 0xE0):
        e[off] = rnd_word(R)
    # position and bounds: plausible so that clamps trigger sometimes
    posx = R.randint(0, 0x4E00000) & M
    posy = R.randint(0, 0x3C00000) & M
    e[0x114], e[0x118] = posx, posy
    k = R.random()
    if k < 0.7:
        e[0x108] = R.randint(-0x100000, 0x200000) & M
        e[0xFC] = R.randint(0x4000000, 0x4E00000)
        e[0x10C] = R.randint(-0x100000, 0x200000) & M
        e[0x100] = R.randint(0x3000000, 0x3C00000)
    else:
        for off in (0x108, 0xFC, 0x10C, 0x100):
            e[off] = rnd_word(R)
    if R.random() < 0.5:
        e[0x13C] = 0
    sh = R.choice([0, 0, 0, 1, 2, 5, 31, 32 + 1, 0xFF, 7])
    return base, e, sh


def run(base, e, sh):
    mp = 0x80200000
    ram = {}
    def w(a, v, n=4):
        for i in range(n):
            ram[a + i] = (v >> (8 * i)) & 0xFF
    for off, v in e.items():
        w(base + off, v)
    w(MAPPTR, mp)
    ram[mp + 8] = sh & 0xFF
    m = Mem(ram, strict=True)
    c = Cpu(m)
    c.run(0x800366FC, (base,))
    return m, c


if __name__ == "__main__":
    R = random.Random(77)
    bad = 0
    N = 4000
    flagset = 0
    for n in range(N):
        base, e, sh = gen(R)
        m, c = run(base, e, sh)
        exp = model(e, sh if sh < 0x80 else sh)  # lb sign irrelevant (&31)
        # extract result writes
        got = {}
        for a, sz, v in m.wlog:
            got[a - base] = v
        for off in (0xCC, 0xD0, 0xDC, 0xE0):
            if got.get(off) != exp[off]:
                bad += 1
                print("MISMATCH", n, hex(off), got.get(off), exp[off]); break
        if (got.get(0x13C) == 1) != (0x13C in exp):
            bad += 1; print("FLAG MISMATCH", n)
        if 0x13C in exp: flagset += 1
        # no other writes
        extra = set(got) - {0xCC, 0xD0, 0xDC, 0xE0, 0x13C}
        if extra:
            print("EXTRA WRITES", extra); bad += 1
        # flag never cleared
        if e[0x13C] == 1 and got.get(0x13C, 1) != 1:
            print("flag cleared!"); bad += 1
    print("push model: runs", N, "mismatches", bad, "flag set in", flagset, "coverage", len(c.visited))
    print("TX", TX)
    print("TY", TY)
