"""xyfuzz.py - replays xymodel.compute_xy against the REAL code of ALUN_CD.EXE (France) run in the mips.py interpreter, on random vectors.
Compares: the return value (v0), every word of every entity block (0x294 bytes, so any extra write shows), and records the executed instruction
addresses of the five functions to report the coverage.  Usage: python -I xyfuzz.py [n] [seed] [mutant]
"""
import sys, os, random, struct, collections
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import mips
from mips import Mem, Cpu, M32, s32
import xymodel as XM

STRIDE = 0x294
ENT0 = XM.HERO
SENT = 0xDEAD0000
SP = 0x801FFE00
TILES = 0x80310000
FUNCS = {
    "ComputeXY 0x80037730": (0x80037730, 0x80037E34),
    "FindCandidate 0x80036F34": (0x80036F34, 0x800370C4),
    "GroundHeight 0x800370C4": (0x800370C4, 0x80037364),
    "WallOther 0x800373E4": (0x800373E4, 0x80037488),
    "WallHero 0x80037488": (0x80037488, 0x800375E0),
}


class CovCpu(Cpu):
    cov = set()

    def step(self):
        CovCpu.cov.add(self.pc)
        return Cpu.step(self)

    def step_plain(self):
        CovCpu.cov.add(self.pc)
        return Cpu.step_plain(self)


def ent_addr(i):
    return ENT0 + STRIDE * i


def load_world(w):
    """write the model world into a fresh Mem"""
    m = Mem()
    m.ww(0x800DD86C, TILES)
    for x, v in w.xtab.items():
        m.wh(0x80127168 + 2 * x, v & 0xFFFF)
    for (col, row), (attr, slope, hb) in w.cell.items():
        a = TILES + 0x604 + row * 416 + col * 8
        m.wh(a, attr); m.wb(a + 2, slope); m.wb(a + 3, hb)
    m.ww(0x80127000, w.gravflag & M32)
    m.ww(0x80127164, w.locktimer & M32)
    m.ww(0x800DC058, w.dbg58 & M32)
    m.ww(0x800DC05C, w.dbg5c & M32)
    for i, a in enumerate(w.collidables):
        m.ww(0x80127C28 + 4 * i, a)
    m.ww(0x80127D2C, len(w.collidables))
    for a, E in w.ents.items():
        for off, v in E.items():
            m.ww(a + off, v & M32)
    return m


def run_real(w, a):
    m = load_world(w)
    cpu = CovCpu(m, {})
    cpu.r[29] = SP
    cpu.r[4] = a
    m.ww(SP + 0x1C, SENT)
    cpu.run(0x80037730, SENT, max_steps=2_000_000)
    return s32(cpu.r[2]), m, cpu.steps


def compare(w, a, ret_model, ret_real, m):
    diffs = []
    if (ret_model & M32) != (ret_real & M32):
        diffs.append(("ret", ret_model, ret_real))
    for addr, E in w.ents.items():
        for off in range(0, STRIDE, 4):
            mv = s32(E.get(off, 0))
            rv = s32(m.rw(addr + off))
            if mv != rv:
                diffs.append((hex(addr), hex(off), mv, rv))
    return diffs


def clone_world(w):
    import copy
    return copy.deepcopy(w)


def gen_world(rnd):
    w = XM.World()
    w.tile_base = TILES
    for x in range(0, 1248):
        w.xtab[x] = min(x // 24, 51)
    hero_mode = rnd.random() < 0.35
    w.gravflag = rnd.choice([0, 1, 2, 3, 128])
    w.locktimer = rnd.choice([0, 0, 0, 0x20, 5])
    w.dbg58 = rnd.choice([0, 0, 0, 0, 0x80000000 - (1 << 32)])        # bit 31 set sometimes
    w.dbg5c = rnd.choice([0, 0, 0x80000000, 0x7FFFFFFF])
    x0 = rnd.randint(150, 1000)
    y0 = rnd.randint(60, 800)
    if rnd.random() < 0.2:           # map edges: small x, rows 0 and >= 0x3c, negative y (clamps of 0x800370C4)
        x0 = rnd.choice([rnd.randint(40, 56), rnd.randint(1150, 1190)])
        y0 = rnd.choice([rnd.randint(-30, 20), rnd.randint(930, 1000), rnd.randint(60, 800)])
    col0, row0 = x0 // 24, y0 // 16
    density = rnd.choice([0.0, 0.05, 0.15, 0.4, 0.8])
    hmax = rnd.choice([0, 1, 3, 6])
    attrs = [0x40, 0x41, 0x1000, 0x1040, 0x800, 0x600, 0xE00, 0x0A00, 0x01, 0x8000, 0x200]
    for c in range(col0 - 4, col0 + 5):
        for r in range(row0 - 4, row0 + 5):
            attr = rnd.choice(attrs) if rnd.random() < density else rnd.choice([0, 0, 0, 0x800, 0x600, 0x200])
            slope = rnd.choice([0, 0, 0, 0, 1, 2, 3]) if rnd.random() < 0.3 else 0
            hb = rnd.randint(0, hmax) if hmax else 0
            if rnd.random() < 0.15:
                hb = rnd.randint(0, 2)
            w.cell[(min(max(c, 0), 51), min(max(r, 0), 59))] = (attr, slope, hb)
    # the subject
    a = XM.HERO if hero_mode else ent_addr(rnd.randint(1, 6))
    n_other = rnd.choice([0, 0, 1, 2, 5])
    addrs = [ent_addr(i) for i in range(0, 8)]
    for ad in addrs:
        w.ents[ad] = {}
    mode_ground = rnd.random() < 0.55
    wd = rnd.choice([6, 12, 21, 24, 30, 40])
    hd = rnd.choice([6, 10, 15, 16, 24])
    dd = rnd.choice([0, 8, 16, 24, 32])
    ox = -wd // 2
    oy = -hd // 2
    oz = rnd.choice([0, 0, 0, 2, -2])
    flags = 0
    if mode_ground:
        flags |= 0x100
    if rnd.random() < 0.55: flags |= 0x80
    if rnd.random() < 0.3: flags |= 0x2000
    if rnd.random() < 0.4: flags |= 0x8
    if rnd.random() < 0.3: flags |= 0x1
    pz_tiles = rnd.choice([0, 1, 2, 3, 5])
    pz = (pz_tiles * 16 << 16) + 1 + rnd.choice([0, 0, 0, 65536, -65536, 3 * 65536 - 1, 3 * 65536, -3 * 65536 - 2, -3 * 65536 - 3, 5 << 16, 40000])
    px = (x0 << 16) + rnd.choice([0, 0, rnd.randint(0, 65535)])
    py = (y0 << 16) + rnd.choice([0, 0, rnd.randint(0, 65535)])
    forces = [0, 1, -1, 2, -2, 3, 7, -7, 65535, 65536, -65536, 159744, -159744, 106496, 53248, 0x8000, -0x8000, 0xC000, -0xC000, 80000, -80000, 1 << 18, -(1 << 18)]
    def rf():
        return rnd.choice(forces + [rnd.randint(-(1 << 19), 1 << 19)] * 3)
    fx, fy = rf(), rf()
    if rnd.random() < 0.3: fy = 0
    if rnd.random() < 0.15: fx = 0
    if rnd.random() < 0.5:         # cardinal-ish
        d = rnd.choice([0, 8, 16, 24, 0, 8, 16, 24, 32, 40])
    else:
        d = rnd.randint(0, 33)
    for ad in addrs:
        E = w.ents[ad]
        E[0x6c] = 0
        E[0x1e4], E[0x1e8], E[0x1ec] = ox << 16, oy << 16, oz << 16
        E[0x1f0], E[0x1f4], E[0x1f8] = (wd << 16) - 1, (hd << 16) - 1, ((dd << 16) - 1) if dd else 0
    S = w.ents[a]
    S[0x6c] = flags
    S[0x8c] = d
    S[0xe4], S[0xe8] = fx, fy
    S[0x114], S[0x118], S[0x11c] = px, py, pz
    S[0xb8] = rnd.choice([0, 0, 0, 0, 65536, -65536])
    S[0xb4] = rnd.choice([0, 0, 0, 0x80])
    S[0x28] = 0 if rnd.random() < 0.9 else ent_addr(7)
    S[0x13c] = rnd.choice([0, 0, 1])
    S[0x138] = rnd.randint(0, 100)
    XM.set_mod(S)
    if a != XM.HERO:
        pass
    # the hero entity itself must be a valid block as well (its fields feed the hero wall test when the subject is the hero)
    # other colliders: placed around the subject so that overlaps are frequent
    cols = []
    others = [x for x in addrs if x != a]
    rnd.shuffle(others)
    for ad in others[:n_other]:
        E = w.ents[ad]
        E[0x6c] = 0x80
        ow = rnd.choice([6, 12, 24, 48])
        oh = rnd.choice([8, 16, 32])
        od = rnd.choice([0, 8, 16, 32])
        E[0x1e4], E[0x1e8], E[0x1ec] = (-ow // 2) << 16, (-oh // 2) << 16, 0
        E[0x1f0], E[0x1f4], E[0x1f8] = (ow << 16) - 1, (oh << 16) - 1, ((od << 16) - 1) if od else 0
        dx = rnd.choice([-1, 1]) * rnd.randint(0, 60)
        dy = rnd.choice([-1, 1]) * rnd.randint(0, 40)
        E[0x114] = px + (dx << 16)
        E[0x118] = py + (dy << 16)
        E[0x11c] = pz + rnd.choice([0, 0, 65536 * 4, -65536 * 4, 65536 * 20])
        XM.set_mod(E)
        cols.append(ad)
    if flags & 0x80 and rnd.random() < 0.8:
        cols.append(a)
    rnd.shuffle(cols)
    w.collidables = cols
    # hero block must have wall-test data when a != hero: nothing to do (not used)
    return w, a


def gen_world_focus(rnd):
    """focused generator: an entity walking on flat ground (the 'ground' branch is reachable) towards one or two crates whose Z box overlaps the
    entity at its own Z but not always at the snapped Z (terrain + 1): the cases where s4 is assigned or left stale."""
    w = XM.World()
    w.tile_base = TILES
    for x in range(0, 1248):
        w.xtab[x] = min(x // 24, 51)
    w.gravflag = 3
    for c in range(0, 52):
        for r in range(0, 60):
            w.cell[(c, r)] = (0, 0, 0)
    if rnd.random() < 0.3:
        for r in range(0, 60):
            w.cell[(rnd.randint(20, 40), r)] = (rnd.choice([0x40, 0x41]), 0, 0)
    addrs = [ent_addr(i) for i in range(0, 5)]
    for ad in addrs:
        w.ents[ad] = {}
        E = w.ents[ad]
        E[0x6c] = 0
        E[0x1e4], E[0x1e8], E[0x1ec] = -10 << 16, -7 << 16, 0
        E[0x1f0], E[0x1f4], E[0x1f8] = (21 << 16) - 1, (15 << 16) - 1, (rnd.choice([4, 6, 8, 32]) << 16) - 1
    a = ent_addr(1) if rnd.random() < 0.7 else XM.HERO
    S = w.ents[a]
    S[0x6c] = 0x180 | rnd.choice([0, 0, 0x8, 0x1, 0x2000])
    d = rnd.choice([0, 8, 16, 24, 24, 8])
    S[0x8c] = d
    fmag = rnd.choice([159744, 196608, 79872, 393216, 56473, 33333, 7])
    sign = -1 if d in (8, 16) else 1
    fx, fy = (fmag * sign, 0) if d in (8, 24) else (0, fmag * sign)
    if d == 0 and rnd.random() < 0.5:
        fy = fmag
    S[0xe4], S[0xe8] = fx, fy
    px, py = (rnd.randint(200, 700) << 16) + rnd.randint(0, 65535), (rnd.randint(200, 600) << 16) + rnd.randint(0, 65535)
    delta = rnd.choice([0, 0, 1, 2, -1, -2, 0])
    pz = 1 + delta * 65536 + rnd.choice([0, 0, 1, -1])
    S[0x114], S[0x118], S[0x11c] = px, py, pz
    S[0xb8] = 0
    XM.set_mod(S)
    cols = [a]
    for ad in [x for x in addrs if x != a][:rnd.choice([1, 1, 2])]:
        E = w.ents[ad]
        E[0x6c] = 0x80
        ow, oh = rnd.choice([12, 24]), rnd.choice([8, 16])
        E[0x1e4], E[0x1e8], E[0x1ec] = (-ow // 2) << 16, (-oh // 2) << 16, 0
        E[0x1f0], E[0x1f4] = (ow << 16) - 1, (oh << 16) - 1
        E[0x1f8] = (rnd.choice([8, 16, 32, 48]) << 16) - 1
        ahead = rnd.randint(8, 40) * 65536 + rnd.randint(0, 65535)
        E[0x114] = px + (ahead if fx > 0 else -ahead if fx < 0 else rnd.randint(-5, 5) * 65536)
        E[0x118] = py + (ahead if fy > 0 else -ahead if fy < 0 else rnd.randint(-5, 5) * 65536)
        E[0x11c] = pz + rnd.choice([0, 0, 2, 3, 4, 5, -3, -4]) * 65536 + rnd.choice([0, 1, -1])
        XM.set_mod(E)
        cols.append(ad)
    rnd.shuffle(cols)
    w.collidables = cols
    return w, a


def main():
    n = int(sys.argv[1]) if len(sys.argv) > 1 else 300
    seed = int(sys.argv[2]) if len(sys.argv) > 2 else 1
    rnd = random.Random(seed)
    # stairs table from the EXE
    bad = 0
    stats = collections.Counter()
    shown = 0
    for k in range(n):
        w, a = (gen_world_focus if "focus" in sys.argv else gen_world)(rnd)
        w.stairs = [s32(mips.Mem().rw(0x800236D4 + 4 * i)) for i in range(24)]
        w2 = clone_world(w)
        trace = []
        rm = XM.compute_xy(w2, a, trace)
        rr, m, steps = run_real(w, a)
        diffs = compare(w2, a, rm, rr, m)
        stats["vectors"] += 1
        stats["attempts"] += len(trace)
        stats["hero"] += (a == XM.HERO)
        stats["fa_set"] += bool(w2.ents[a].get(0x13c, 0) and not w.ents[a].get(0x13c, 0))
        stats["ret_nonzero"] += (rr != 0)
        stats["multi_attempt"] += (len(trace) > 1)
        if diffs:
            bad += 1
            if shown < 5:
                shown += 1
                print("MISMATCH vector", k, "subject", hex(a), diffs[:6])
    print("vectors", n, "mismatching", bad, dict(stats))
    # coverage
    for name, (lo, hi) in FUNCS.items():
        total = (hi - lo) // 4
        hit = sorted(p for p in CovCpu.cov if lo <= p < hi)
        miss = [p for p in range(lo, hi, 4) if p not in CovCpu.cov]
        print("coverage %-28s %d / %d instructions" % (name, len(hit), total))
        if miss and "-v" in sys.argv:
            print("   uncovered:", " ".join("%08x" % p for p in miss))
    return bad


if __name__ == "__main__":
    sys.exit(1 if main() else 0)
