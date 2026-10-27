"""h4_vec_drivers.py - the item-driver wrapper 0x8003634C for the eight tomes (and the cancel handlers it reaches through the slope helper 0x800351B8),
REAL code vs the models of h4_models.py, on random vectors.  After each call the written regions of RAM are compared BYTE FOR BYTE (entity slots, the
globals page 0x80127100-0x80127200, the 9-word shake block, the random word, the pools of fake entities/effects), plus the return value and the journal of the
stubbed calls (spawn, effect, sound).

usage: python -B h4_vec_drivers.py [N] [seed] [item]   exit code 0 iff no mismatch.  Also prints the coverage of the instructions of the real functions."""
import random
import sys
from collections import Counter
import spellworld as S
import world as W
from machine import s32, M32
import h4_models as M

HERO = M.HERO
STRIDE = M.STRIDE
SPAWN_POOL = 0x801EC000        # fake entities returned by the spawn stub (0x294 bytes each)
FX_POOL = 0x801EE000           # fake effect entities (0x100 bytes each)
SHEET_BASE = 0x801E7000        # synthetic balance sheets (0x20 bytes each): byte 2+type = response
N_SPAWN = 24
N_FX = 40
COMPARE = [(0x80127D30, 0x80127D30 + 64 * STRIDE), (0x80127100, 0x80127200), (0x800E4338, 0x800E4338 + 0x24), (0x80098700, 0x80098710),
           (SPAWN_POOL, SPAWN_POOL + N_SPAWN * STRIDE), (FX_POOL, FX_POOL + N_FX * 0x100), (0x80134360, 0x80134364)]

TOMES = list(range(0x2B, 0x33))
TICKS = {0x2B: [20, 25, 30, 35, 40, 45, 50, 55, 60, 65, 70, 75, 80, 85, 90, 95], 0x2C: [16, 32, 48, 64, 80, 96, 112, 128, 144, 160, 176, 192, 224, 240],
         0x2D: [40, 60, 80], 0x2E: [40, 60, 80, 100, 120, 140], 0x31: [30, 40, 50, 60, 70, 80, 90, 100]}


class Ctx:
    def __init__(self):
        self.j = []
        self.spawn_results = []
        self.fx_results = []


def install_hooks(m, ctx):
    def args(mm, n_stack):
        sp = mm.r[29]
        return [mm.r[4] & M32, mm.r[5] & M32, mm.r[6] & M32, mm.r[7] & M32] + [mm.peek(sp + 0x10 + 4 * i) & M32 for i in range(n_stack)]

    def spawn(mm):
        a = args(mm, 3)
        ctx.j.append(('spawn', a[0], a[1], a[2], a[3], a[4], a[5], a[6]))
        mm.r[2] = ctx.spawn_results.pop(0) if ctx.spawn_results else 0

    def fx(mm):
        a = args(mm, 2)
        ctx.j.append(('fx', a[0], a[1], a[2], a[3], a[4], a[5]))
        mm.r[2] = ctx.fx_results.pop(0) if ctx.fx_results else 0

    def snd(mm):
        ctx.j.append(('sound', mm.r[4] & M32))
        mm.r[2] = 0
    m.hooks[0x80039F58] = spawn
    m.hooks[0x8003BDD8] = fx
    m.hooks[0x800490FC] = snd


def build_machine():
    m, STATS, placed = S.make(item=None, indices=(1,))
    ctx = Ctx()
    install_hooks(m, ctx)
    # synthetic balance sheets: 16 sheets of 0x20 bytes, byte 2 + t = the response to attack type t (t = 0..13)
    return m, STATS, ctx


def mk_sheets(m, rnd):
    for k in range(16):
        base = SHEET_BASE + 0x20 * k
        for b in range(0x20):
            m.poke8(base + b, 0)
        for t in range(0, 14):
            r = rnd.choice([0x10, 0x10, 0x20, 0x08, 0x80, 0xC0, 0x40, 0x00, 0x1F, 0x90])
            m.poke8(base + 2 + t, r)


def populate(m, rnd, item, phase, counter):
    """random world for one vector; returns a descriptor dict"""
    mk_sheets(m, rnd)
    normal = rnd.random() < 0.8                         # the casting regime: hero in 0x32/0x33, slope 0..3
    hero_anim = rnd.choice([0x33, 0x33, 0x33, 0x32]) if normal else rnd.choice([0x33, 0x32, 0x34, 0x31, 0, 0x2D, 0x3A])
    if phase == 2:
        hero_anim = rnd.choice([0x34, 0x34, 0x0, 0x33, 0x2D, 0x32])
    m.poke(HERO + 0x88, hero_anim)
    m.poke(HERO + 0x8C, rnd.randrange(0, 32))
    m.poke(HERO + 0x114, rnd.randrange(50 << 16, 400 << 16))
    m.poke(HERO + 0x118, rnd.randrange(50 << 16, 400 << 16))
    m.poke(HERO + 0x11C, rnd.randrange(0, 40 << 16) & ~0xFFFF | rnd.choice([1, 0, 0x8000]))
    hpmax = rnd.choice([10, 12, 20, 30, 50, 7, 3])
    m.poke(HERO + 0x18, hpmax)
    m.poke(HERO + 0x14, rnd.randrange(0, hpmax + 1))
    m.poke(HERO + 0x18C, (4 if rnd.random() < 0.1 else rnd.randrange(0, 4)) if not normal else rnd.randrange(0, 4))
    m.poke(M.INUSE, item)
    m.poke(M.COUNTER, counter)
    m.poke(M.PHASE, phase)
    m.poke(M.RNG, rnd.getrandbits(32))
    # globals of the drivers
    m.poke(0x80127118, rnd.choice([0, 5, 0x1D, 0x1D, 0x1D, 0x1E, 0x1F, 0x3A, 0x3A, 0x3B, 0x3B, 0x3C, 0x3D, rnd.randrange(0, 100)]))
    for a in (0x8012711C, 0x80127120, 0x80127124, 0x80127128, 0x8012712C, 0x80127130, 0x80127134):
        m.poke(a, 0)
    for k in range(9):
        m.poke(M.SHAKE + 4 * k, rnd.getrandbits(32))
        m.poke(M.SHAKE_BAK + 4 * k, rnd.getrandbits(32))
    # entity slots 1..n
    n = rnd.choice([0, 1, 2, 3, 4, 5, 6, 8]) if item != 0x31 else rnd.choice([0, 1, 2, 3, 4, 5, 6, 8, 8, 10, 12])
    last = n
    for i in range(0, 64):
        e = M.ent(i)
        if i == 0:
            continue
        for off in range(0, STRIDE, 4):
            m.poke(e + off, 0)
    pop = []
    for i in range(1, n + 1):
        e = M.ent(i)
        status = rnd.choice([2, 2, 2, 3, 0, 4, 1]) if item != 0x31 else rnd.choice([2, 2, 2, 2, 3, 0, 4])
        typ = rnd.choice([0x11, 0x13, 0x14, 0x15, 0x17, 0x18, 0x13, 0x18, 0x11, 5, 0x100, 0x2A])
        m.poke(e + 0x10, status)
        m.poke(e + 0x68, typ)
        m.poke(e + 4, rnd.randrange(1, 1000))
        m.poke(e + 0x20, 0 if rnd.random() < 0.92 else 5)
        m.poke(e + 0x6C, rnd.choice([1, 1, 1, 1, 1, 0, 0x81, 0x14, 0x101]))
        m.poke(e + 0x1D0, 0 if rnd.random() < 0.92 else 3)
        m.poke(e + 0x1CC, 0 if rnd.random() < 0.92 else 9)
        m.poke(e + 0xB4, 0 if rnd.random() < 0.92 else 0x40 | rnd.choice([0, 1]))
        m.poke(e + 0x1C4, 0 if rnd.random() < 0.05 else SHEET_BASE + 0x20 * rnd.randrange(16))
        m.poke(e + 0x114, (m.peek(HERO + 0x114) + rnd.randrange(-200 << 16, 200 << 16)) & M32)
        m.poke(e + 0x118, (m.peek(HERO + 0x118) + rnd.randrange(-200 << 16, 200 << 16)) & M32)
        m.poke(e + 0x11C, (m.peek(HERO + 0x11C) + rnd.randrange(-100 << 16, 120 << 16)) & M32)
        if rnd.random() < 0.25:                         # boundary distances of the targeting rules: squared distance 0x6400, 0x6401 (wind 1) from the hero
            ox, oy = rnd.choice([(160, 0), (0, 160), (96, 128), (128, 96), (160, 1), (1, 160), (120, 0), (0, 120), (72, 96), (120, 1)])
            ox *= rnd.choice([-1, 1])
            oy *= rnd.choice([-1, 1])
            m.poke(e + 0x114, (m.peek(HERO + 0x114) + (ox << 16)) & M32)
            m.poke(e + 0x118, (m.peek(HERO + 0x118) + (oy << 16)) & M32)
        if item == 0x31 and rnd.random() < 0.6:           # a clean candidate of the wind query (status 2, ClassA, no blocker, a plain sheet), close to the hero
            m.poke(e + 0x10, 2)
            m.poke(e + 0x20, 0)
            m.poke(e + 0x6C, 1)
            m.poke(e + 0x1D0, 0)
            m.poke(e + 0x1CC, 0)
            m.poke(e + 0xB4, 0)
            m.poke(e + 0x1C4, SHEET_BASE + 0x20 * 0)
            if rnd.random() < 0.7:
                m.poke(e + 0x114, (m.peek(HERO + 0x114) + rnd.randrange(-120 << 16, 120 << 16)) & M32)
                m.poke(e + 0x118, (m.peek(HERO + 0x118) + rnd.randrange(-120 << 16, 120 << 16)) & M32)
                m.poke(e + 0x11C, (m.peek(HERO + 0x11C) + rnd.randrange(-20 << 16, 70 << 16)) & M32)
        m.poke(e + 0x290, rnd.choice([0, 1, 5, 100, -3 & M32, 340]))
        m.poke(e + 0x1B8, 0)
        m.poke(e + 0x28, 0)
        pop.append(i)
    if rnd.random() < 0.15:
        last = max(0, n + rnd.choice([-1, 1]))
    m.poke(0x80134360, last)
    # pools
    m.zero(SPAWN_POOL, N_SPAWN * STRIDE)
    m.zero(FX_POOL, N_FX * 0x100)
    sres, fres = [], []
    for k in range(N_SPAWN):
        a = SPAWN_POOL + k * STRIDE
        m.poke(a + 4, 5000 + k)
        hz = m.peek(HERO + 0x11C)
        m.poke(a + 0x138, rnd.choice([0, 0x100000, 0x2000000, 0x1800000, rnd.randrange(0, 40 << 16), (hz + 0x1000000) & M32, (hz + 0x1000000) & M32, (hz + 0x1000001) & M32, (hz + 0xFFFFFF) & M32]))
        m.poke(a + 0x144, rnd.choice([0, 0, 1]))
        sres.append(a if rnd.random() > 0.08 else 0)
    for k in range(N_FX):
        a = FX_POOL + k * 0x100
        m.poke(a + 0x3C, rnd.randrange(0, 1 << 20))
        m.poke(a + 0x40, rnd.randrange(0, 1 << 20))
        fres.append(a if rnd.random() > 0.08 else 0)
    # pointers/ids for the "alive entity" tests: fire1 (0x8012711C/0x80127120), fire2 (0x80127124/0x80127128), wind2 (0x80127130/0x80127134)
    alive = rnd.random() < 0.6
    for (pa, ia) in ((0x8012711C, 0x80127120), (0x80127124, 0x80127128), (0x80127130, 0x80127134)):
        pe = SPAWN_POOL + (N_SPAWN - 1) * STRIDE if False else (M.ent(rnd.randrange(1, 4)) if n >= 1 else SPAWN_POOL)
        m.poke(pa, pe)
        m.poke(ia, m.peek(pe + 4) if alive else (m.peek(pe + 4) + 1) & M32)
    if rnd.random() < 0.6 and n >= 1:
        m.poke(0x8012712C, M.ent(rnd.randrange(1, n + 1)))
    if item == 0x31 and phase == 1 and rnd.random() < 0.3:     # the boundary family of the wind 1 targeting: one clean candidate at squared distance 0x63FF..0x6401 (limit 0x6401) in the casting state
        m.poke(HERO + 0x88, 0x33)
        m.poke(HERO + 0x18C, rnd.randrange(0, 4))
        e = M.ent(1)
        for off in range(0, STRIDE, 4):
            m.poke(e + off, 0)
        m.poke(e + 0x10, 2)
        m.poke(e + 4, 77)
        m.poke(e + 0x6C, 1)
        m.poke(e + 0x1C4, SHEET_BASE)
        ox, oy = rnd.choice([(160, 0), (0, 160), (96, 128), (128, 96), (160, 1), (1, 160), (159, 0), (0, 159), (160, -1)])
        m.poke(e + 0x114, (m.peek(HERO + 0x114) + (ox << 16)) & M32)
        m.poke(e + 0x118, (m.peek(HERO + 0x118) + (oy << 16)) & M32)
        m.poke(e + 0x11C, m.peek(HERO + 0x11C))
        m.poke(0x80134360, 2)
        m.poke(0x8012712C, 0)
        for k in range(14):
            m.poke8(SHEET_BASE + 2 + k, 0x10)
        n = 1
        last = 2
    return dict(hero_anim=hero_anim, n=n, last=last, sres=sres, fres=fres)


def run_vector(m, ctx, rnd, snapshot, item, phase, counter):
    m.ram[:] = snapshot[0]
    m.init[:] = snapshot[1]
    m.journal.clear()
    d = populate(m, rnd, item, phase, counter)
    ram0 = bytes(m.ram)
    # model on a copy
    mem = M.Mem(ram0)
    gdt = None
    menv = M.Env(mem, d['sres'], d['fres'], gdt)
    mres = M.wrapper(menv)
    # real
    ctx.j = []
    ctx.spawn_results = list(d['sres'])
    ctx.fx_results = list(d['fres'])
    rres = s32(m.call(0x8003634C, limit=3_000_000))
    diffs = []
    if rres != mres:
        diffs.append(('return', mres, rres))
    if ctx.j != menv.j:
        diffs.append(('journal', menv.j, ctx.j))
    for (a, b) in COMPARE:
        ra = m.ram[a & 0x1FFFFFFF:b & 0x1FFFFFFF]
        ma = mem.b[a & 0x1FFFFFFF:b & 0x1FFFFFFF]
        if ra != ma:
            for k in range(0, len(ra)):
                if ra[k] != ma[k]:
                    diffs.append(('mem', hex(a + k), ma[k], ra[k]))
                    break
    return diffs, mres, d


def main(N=2000, seed=20261017, items=None, mut=None):
    rnd = random.Random(seed)
    m, STATS, ctx = build_machine()
    if mut:
        mut(m)
    snapshot = (bytes(m.ram), bytes(m.init))
    m.cov.clear()
    cnt = Counter()
    mism = 0
    first = []
    items = items or TOMES
    for n in range(N):
        item = rnd.choice(items)
        ph = rnd.choice([0, 0, 0, 1, 1, 1, 2, 2, 3])
        # counters near the interesting values
        tk = TICKS.get(item, [20, 100, 0x118])
        if ph == 0:
            base = rnd.choice(tk + tk + tk + [0, 1, 150, 300])
            counter = max(0, base + rnd.choice([0, 0, 0, 0, 0, -1, 1, 2]))
        elif ph == 1:
            counter = rnd.choice([0, 5, 7, 8, 15, 16, 99, 100, 101, 0x117, 0x118, 0x119, 250, 33, 64, rnd.randrange(0, 400)] + tk)
        else:
            counter = rnd.randrange(0, 400)
        diffs, mres, d = run_vector(m, ctx, rnd, snapshot, item, ph, counter)
        cnt['item %#x phase %d result %d' % (item, ph, mres)] += 1
        if diffs:
            mism += 1
            if len(first) < 6:
                first.append((item, ph, counter, d['hero_anim'], diffs[:3]))
    return mism, cnt, first, set(m.cov)


if __name__ == '__main__':
    N = int(sys.argv[1]) if len(sys.argv) > 1 else 2000
    seed = int(sys.argv[2]) if len(sys.argv) > 2 else 20261017
    items = [int(sys.argv[3], 0)] if len(sys.argv) > 3 else None
    mism, cnt, first, cov = main(N, seed, items)
    print('vectors', N, 'seed', seed, 'mismatches', mism)
    for k, v in sorted(cnt.items()):
        print('  ', v, k)
    for f in first:
        print('MISMATCH', f)
    regions = {'wrapper 0x8003634C': (0x8003634C, 0x800364C0), 'slope helper': (0x800351B8, 0x80035204), 'earth1': (0x80035320, 0x800354D0), 'earth2': (0x800354D0, 0x80035790),
               'water': (0x80035790, 0x80035A84), 'fire1': (0x80035A84, 0x80035C64), 'fire2': (0x80035C64, 0x80035EB0), 'wind1': (0x80035EB0, 0x80036218), 'wind2': (0x80036218, 0x8003634C),
               'cancel earth2': (0x80034BDC, 0x80034C54), 'cancel water': (0x80034C54, 0x80034D2C), 'cancel fire1': (0x80034D2C, 0x80034E08), 'cancel fire2': (0x80034E08, 0x80034EC4),
               'cancel wind2': (0x80034EC4, 0x800350C0), 'cancel dispatch': (0x800350C0, 0x800351B8), 'heal water': (0x80032F84, 0x800330FC), 'heal full': (0x80033274, 0x800333AC),
               'query': (0x8003AF70, 0x8003B0E8), 'sort': (0x8003AE04, 0x8003AF70)}
    for name, (a, b) in regions.items():
        tot = (b - a) // 4
        hit = sum(1 for x in range(a, b, 4) if x in cov)
        print('coverage %-16s %3d/%3d' % (name, hit, tot))
    sys.exit(1 if mism else 0)
