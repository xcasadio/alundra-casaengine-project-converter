"""crit_emu2.py - critic: independent replay of ComputeXYPosition 0x80037730 in the SECOND interpreter (emu_g1a/emu.py, a different code base than mips.py),
with scenario code written from scratch (not xyscen/xymodel).  Prints, for the east wall and the crate cases, the same statistics the reader reports.
Usage: python -I crit_emu2.py
"""
import sys, os, collections
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(HERE, "..", "..", "work", "emu_g1a"))
sys.path.append(r"C:/Users/casad/AppData/Roaming/Python/Python314/site-packages")
import emu as EMU

U = 65536
TILES = 0x80180000          # inside the 2 MB RAM of the second interpreter
HERO = 0x80127D30
STRIDE = 0x294
SUBJ = HERO + STRIDE * 1
CRATE = HERO + STRIDE * 2
STAIRS = 0x800236D4


def s32(v):
    v &= 0xFFFFFFFF
    return v - (1 << 32) if v & 0x80000000 else v


def make():
    e = EMU.Emu()
    e.ww(0x800DD86C, TILES)
    for x in range(0, 1248):
        e.wh(0x80127168 + 2 * x, min(x // 24, 51))
    return e


def cell(e, col, row, attr, slope=0, hb=0):
    a = TILES + 0x604 + row * 416 + col * 8
    e.wh(a, attr); e.wb(a + 2, slope); e.wb(a + 3, hb)


def wipe_cells(e):
    for c in range(52):
        for r in range(60):
            cell(e, c, r, 0)


def clear_entity(e, a):
    for off in range(0, STRIDE, 4):
        e.ww(a + off, 0)


def put(e, a, flags, pos, off=(-10, -7, 0), size=(21, 15), depth=32, d=24):
    clear_entity(e, a)
    e.ww(a + 0x1e4, off[0] * U); e.ww(a + 0x1e8, off[1] * U); e.ww(a + 0x1ec, off[2] * U)
    e.ww(a + 0x1f0, size[0] * U - 1); e.ww(a + 0x1f4, size[1] * U - 1); e.ww(a + 0x1f8, depth * U - 1)
    e.ww(a + 0x6c, flags); e.ww(a + 0x8c, d)
    e.ww(a + 0x114, pos[0]); e.ww(a + 0x118, pos[1]); e.ww(a + 0x11c, pos[2])
    set_mod(e, a)


def set_mod(e, a):
    e.ww(a + 0x1d8, e.rws(a + 0x114) + e.rws(a + 0x1e4))
    e.ww(a + 0x1dc, e.rws(a + 0x118) + e.rws(a + 0x1e8))
    e.ww(a + 0x1e0, e.rws(a + 0x11c) + e.rws(a + 0x1ec))


def set_collidables(e, lst):
    for i, a in enumerate(lst):
        e.ww(0x80127C28 + 4 * i, a)
    e.ww(0x80127D2C, len(lst))


def tick(e, a, fx, fy):
    e.ww(a + 0xe4, fx); e.ww(a + 0xe8, fy); e.ww(a + 0x13c, 0)
    x0 = e.rws(a + 0x114)
    ret = s32(e.call(0x80037730, a))
    return dict(dx=e.rws(a + 0x114) - x0, ret=ret, fa=e.rws(a + 0x13c), x=e.rws(a + 0x114))


def wall_east(e):
    wipe_cells(e)
    for c in range(5, 52):
        for r in range(60):
            cell(e, c, r, 0x40)


def study_wall(F):
    FMAX = 109 * U
    e = make()
    wall_east(e)
    step = max(1, F // 997)
    Ds = list(range(1, F, step)) + list(range(1, 40)) + [F - 1]
    dh = collections.Counter(); fa1 = collections.Counter()
    for D in Ds:
        put(e, SUBJ, 0, (FMAX - D, 100 * U, 1))
        set_collidables(e, [])
        r = [tick(e, SUBJ, F, 0) for _ in range(5)]
        dh[D - r[0]["dx"]] += 1
        fa1[next((i + 1 for i, t in enumerate(r) if t["fa"]), 0)] += 1
    print("wall east F=%d starts=%d deficit=%s firstFA=%s" % (F, len(Ds), dict(sorted(dh.items())), dict(sorted(fa1.items()))))


def study_crate(F, mode):
    CRATE_LEFT = 130 * U
    FMAX = CRATE_LEFT + 10 * U - 21 * U
    e = make()
    wipe_cells(e)
    Ds = list(range(1, F, max(1, F // 997))) + list(range(1, 40))
    cnt = [collections.Counter() for _ in range(4)]
    for D in Ds:
        pz = 16 * U + 1 if mode == "support" else 1
        flags = 0x80 | (0x100 if mode in ("ground", "support") else 0)
        put(e, SUBJ, flags, (FMAX - D, 100 * U, pz))
        put(e, CRATE, 0x80, (CRATE_LEFT + 12 * U, 100 * U, 0), off=(-12, -8, 0), size=(24, 16), depth=48, d=0)
        set_collidables(e, [CRATE, SUBJ])
        r = [tick(e, SUBJ, F, 0) for _ in range(4)]
        for i in range(4):
            cnt[i][r[i]["ret"] != 0] += 1
    n = len(Ds)
    print("crate %s F=%d starts=%d P(ret!=0) t1..t4 = %s" % (mode, F, n, " ".join("%.3f" % (cnt[i][True] / n) for i in range(4))))


if __name__ == "__main__":
    for F in (159744, 196608, 393216, 56473):
        study_wall(F)
    for F in (159744, 196608, 393216):
        for mode in ("air", "support", "ground"):
            study_crate(F, mode)
