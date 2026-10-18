"""c5_camera.py - critic's own models of the sway step 0x8002C894, the camera step 0x8002CDA0 (up to the scroll clamp 0x8002CF38) and E13 0x8007F30C,
run against the real code (cmips).  Seed 99.  Also simulates the life of a Y kick through 6 camera steps."""
import random, sys
from cmips import *

BLK = 0x800E4338
SCX, SCY, FIRST = 0x800E4328, 0x800E432C, 0x800E42B8
KX, KY = 0x800DD870, 0x800DD874
NAMES = ["Flag", "LimX", "LimY", "SpdX", "SpdY", "OffX", "OffY", "ReachX", "ReachY"]


def sway_model(b):
    """b: list of 9 s32; returns new list"""
    f, lx, ly, sx, sy, ox, oy, rx, ry = b
    if f == 0:
        return [f, 0, 0, 0, 0, 0, 0, rx, ry]
    # X
    if lx == 0 or sx == 0:
        ox = 0
    else:
        if rx != 0:
            ox = s32(ox + sx)
            if not (ox < lx):
                ox = lx; rx = 0
        else:
            ox = s32(ox - sx)
            if not (-lx < ox):
                ox = -lx; rx = 1
    # Y
    if ly == 0:
        oy = 0
    else:
        if ry != 0:
            oy = s32(oy + sy)
            if not (oy < ly):
                oy = ly; ry = 0
        else:
            oy = s32(oy - sy)
            if not (-ly < oy):
                oy = -ly; ry = 1
    return [f, lx, ly, sx, sy, ox, oy, rx, ry]


def camera_model(blk, scx, scy, first, kx, ky, tx, ty, tz):
    b = sway_model(blk)
    ox, oy = b[5], b[6]
    if first != 0:
        scx = s32(tx - 0xA0)
        scy = s32((ty - tz) - 0x88)
        first = 0
    else:
        scx = s32(scx + ((s32(tx - (scx + 0xA0)) >> 4) + ox + kx))
        scy = s32(scy + ((s32((ty - tz) - (scy + 0x88)) >> 4) + oy + ky))
    kx = 0; ky = 0
    if scx < 0: scx = 0
    elif not (scx < 0x3A0): scx = 0x39F
    if scy < 0: scy = 0
    elif not (scy < 0x2D0): scy = 0x2CF
    return b, scx, scy, first, kx, ky


def w32(ram, a, v):
    v &= M
    for i in range(4):
        ram[a + i] = (v >> (8 * i)) & 0xFF


def rv(R):
    k = R.random()
    return R.choice([0, 1, 2, 5, 40, 100, -1, -7, -40]) if k < 0.7 else R.randint(-300, 300)


def trial(R):
    ram = {}
    blk = [R.choice([0, 1, 1, 1]), R.choice([0, 3, 8, 50]), R.choice([0, 3, 8, 50]), R.choice([0, 1, 2, 5]), R.choice([0, 1, 2, 5]),
           R.randint(-60, 60), R.randint(-60, 60), R.choice([0, 1]), R.choice([0, 1])]
    scx, scy = R.randint(-100, 1100), R.randint(-100, 900)
    first = R.choice([0, 0, 0, 1])
    kx, ky = R.choice([0, 0, 2, -3, 5]), R.choice([0, 2, 2, 0, 7])
    tx, ty, tz = R.randint(-300, 1400), R.randint(-300, 1400), R.randint(-100, 300)
    for i, v in enumerate(blk):
        w32(ram, BLK + 4 * i, v)
    w32(ram, SCX, scx); w32(ram, SCY, scy); w32(ram, FIRST, first); w32(ram, KX, kx); w32(ram, KY, ky)
    m = Mem(ram, strict=True)
    c = Cpu(m)
    try:
        c.run(0x8002CDA0, (0, tx, ty, tz), stop_at=[0x8002CF38])
    except Incomplete as ex:
        return None
    got = lambda a: m.rd(a, 4)
    # reads after run don't matter (log only)
    mb, mx, my, mf, mkx, mky = camera_model([s32(x) for x in blk], scx, scy, first, kx, ky, tx, ty, tz)
    real_blk = [s32(m.b(BLK + 4 * i) | m.b(BLK + 4 * i + 1) << 8 | m.b(BLK + 4 * i + 2) << 16 | m.b(BLK + 4 * i + 3) << 24) for i in range(9)]
    rd = lambda a: s32(m.b(a) | m.b(a + 1) << 8 | m.b(a + 2) << 16 | m.b(a + 3) << 24)
    ok = (real_blk == mb) and rd(SCX) == mx and rd(SCY) == my and rd(FIRST) == mf and rd(KX) == mkx and rd(KY) == mky
    return ok, c, (blk, scx, scy, first, kx, ky, tx, ty, tz)


if __name__ == "__main__":
    R = random.Random(99)
    N = 3000
    bad = 0
    cov = set()
    for n in range(N):
        r = trial(R)
        if r is None:
            print("incomplete read"); continue
        ok, c, inp = r
        cov |= c.visited
        if not ok:
            bad += 1
            if bad < 4: print("MISMATCH", inp)
    rng = [a for a in range(0x8002C894, 0x8002C9DC, 4)] + [a for a in range(0x8002CDA0, 0x8002CF3C, 4)]
    print("camera+sway: %d trials, mismatches %d, coverage %d/%d" % (N, bad, len([a for a in rng if a in cov]), len(rng)))
    print("uncovered", [hex(a) for a in rng if a not in cov])
    # kick life: equilibrium camera, scroll centered on target, a kick of +2 written by E13 at tick N
    blk = [0] * 9
    scx, scy = 400, 300
    tx, ty, tz = scx + 0xA0, scy + 0x88, 0
    seq = []
    ky = 2
    first = 0
    for k in range(6):
        blk, scx, scy, first, kx, ky = camera_model(blk, scx, scy, first, 0, ky, tx, ty, tz)
        seq.append(scy)
    print("scrollY after each camera step (start 300, target centred, kick +2 once):", seq)
    # does the kick persist if the target moves away continuously? (kick is a one-shot: consumed each call)
