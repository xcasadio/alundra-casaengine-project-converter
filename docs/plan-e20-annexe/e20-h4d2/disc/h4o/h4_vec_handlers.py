"""h4_vec_handlers.py - the native handlers of the tome entities (E1, E3, E13, E28, C63, C65, C75), REAL code vs the models of h4_models_handlers.py, on random vectors.
Same comparison as h4_vec_drivers.py: byte for byte on the entity slots, the globals page, the random word, the pools; plus the journal (effects, destroy calls) and the shake/camera word.

usage: python -B h4_vec_handlers.py [N] [seed] [handler]   exit code 0 iff no mismatch"""
import random
import sys
from collections import Counter
import spellworld as S
from machine import s32, M32
import h4_models as M
import h4_models_handlers as H
import h4_vec_drivers as D

HERO = M.HERO
STRIDE = M.STRIDE
ATK_BASE = 0x801E6000
COMPARE = [(0x80127D30, 0x80127D30 + 64 * STRIDE), (0x80127100, 0x80127200), (0x80098700, 0x80098710), (D.FX_POOL, D.FX_POOL + D.N_FX * 0x100),
           (0x800DD870, 0x800DD878), (0x80134360, 0x80134364)]


def build():
    m, STATS, ctx = D.build_machine()

    def destroy(mm):
        ctx.j.append(('destroy', mm.r[4] & M32, mm.r[5] & M32))
        mm.r[2] = 0
    m.hooks[0x8003A59C] = destroy
    return m, STATS, ctx


def populate(m, rnd, name):
    D.mk_sheets(m, rnd)
    # hero
    m.poke(HERO + 0x114, rnd.randrange(50 << 16, 400 << 16))
    m.poke(HERO + 0x118, rnd.randrange(50 << 16, 400 << 16))
    m.poke(HERO + 0x11C, rnd.randrange(0, 40 << 16))
    m.poke(HERO + 0xC4, rnd.randrange(-(4 << 16), 4 << 16) & M32)
    m.poke(HERO + 0xC8, rnd.randrange(-(4 << 16), 4 << 16) & M32)
    m.poke16(0x80126F28, rnd.choice([0, 0x20, 0x20, 0x10, 0x30, 0x1000]))
    m.poke(M.RNG, rnd.getrandbits(32))
    m.poke(0x800DD874, rnd.choice([0, 7]))
    for i in range(1, 64):
        e = M.ent(i)
        for off in range(0, STRIDE, 4):
            m.poke(e + off, 0)
    n = rnd.choice([1, 2, 3, 4, 5, 6, 8, 10])
    s = rnd.randrange(1, n + 1)
    for i in range(1, n + 1):
        e = M.ent(i)
        m.poke(e + 0x10, rnd.choice([2, 2, 2, 3, 0, 4]) if i != s else 2)
        m.poke(e + 0x68, rnd.choice([0x11, 0x13, 5, 0x100, 0x2A]))
        m.poke(e + 4, rnd.randrange(1, 1000))
        m.poke(e + 0x20, 0 if rnd.random() < 0.92 else 5)
        m.poke(e + 0x6C, rnd.choice([1, 1, 1, 1, 1, 0, 0x81, 0x14, 0x101]))
        m.poke(e + 0x1D0, 0 if rnd.random() < 0.92 else 3)
        m.poke(e + 0x1CC, 0 if rnd.random() < 0.92 else 9)
        m.poke(e + 0xB4, 0 if rnd.random() < 0.92 else 0x40)
        m.poke(e + 0x1C4, 0 if rnd.random() < 0.05 else D.SHEET_BASE + 0x20 * rnd.randrange(16))
        spread = rnd.choice([40, 120, 400, 700])
        m.poke(e + 0x114, (m.peek(HERO + 0x114) + rnd.randrange(-spread << 16, spread << 16)) & M32)
        m.poke(e + 0x118, (m.peek(HERO + 0x118) + rnd.randrange(-spread << 16, spread << 16)) & M32)
        m.poke(e + 0x11C, (m.peek(HERO + 0x11C) + rnd.randrange(-50 << 16, 60 << 16)) & M32)
        m.poke(e + 0x88, rnd.choice([0, 1, 2]))
        m.poke(e + 0x8C, rnd.randrange(0, 32))
        m.poke(e + 0xAC, rnd.choice([0, 1, 1, 2]))
    last = n if rnd.random() > 0.15 else max(0, n + rnd.choice([-1, 1]))
    m.poke(0x80134360, last)
    se = M.ent(s)
    if n >= 2 and rnd.random() < 0.3:                   # boundary distances (squared 0x3840 for the water orb) from the self entity
        others = [i for i in range(1, n + 1) if i != s]
        e = M.ent(rnd.choice(others))
        ox, oy = rnd.choice([(120, 0), (0, 120), (72, 96), (96, 72), (120, 1), (1, 120), (119, 0), (0, 119)])
        m.poke(e + 0x114, (m.peek(se + 0x114) + ((ox * rnd.choice([-1, 1])) << 16)) & M32)
        m.poke(e + 0x118, (m.peek(se + 0x118) + ((oy * rnd.choice([-1, 1])) << 16)) & M32)
    # attack record of the self entity
    attr = {'C63': 0x0A, 'C65': 0x0B, 'C75': 0x09}.get(name, rnd.randrange(0, 16))
    m.poke8(ATK_BASE, attr | (0x80 if rnd.random() < 0.1 else 0))
    m.poke8(ATK_BASE + 1, 32)
    m.poke(se + 0x1C8, ATK_BASE)
    m.poke(se + 0x6C, rnd.choice([0x74, 0x34, 0x44, 0x14, 0x6C]) | rnd.choice([0, 0x100, 0x2000]))
    if name == 'C63':
        m.poke(se + 0x274, rnd.choice([0, 0, 0, 1, 5, 16, 0xFFFFFFFD]))
        tg = 0
        if rnd.random() < 0.5 and n >= 2:
            tg = M.ent(rnd.choice([i for i in range(1, n + 1) if i != s]))
        m.poke(se + 0x278, tg)
        m.poke(se + 0x27C, (m.peek(tg + 4) if tg and rnd.random() < 0.8 else rnd.randrange(1, 1000)) if tg else 0)
    elif name == 'C75':
        p0 = rnd.choice([1024, 2048, 3584, 100, 0])
        p1 = rnd.choice([3072, 3584, 6000, 50, 1024])
        p2 = rnd.choice([16, 32, 8, 100, 0])
        m.poke(se + 0x274, p0)
        m.poke(se + 0x278, p1)
        m.poke(se + 0x27C, p2)
        m.poke(se + 0x280, rnd.choice([7, 8, -6 & M32, 0, 255]))
        m.poke(se + 0x284, rnd.choice([0, 1]))
        m.poke(se + 0x288, rnd.choice([0, 100, 1024, 3000, 3072, 3584, 5000, -20 & M32]))
        m.poke(se + 0x28C, rnd.randrange(0, 256) if rnd.random() < 0.9 else rnd.randrange(-255, 0) & M32)
        m.poke(se + 0x290, rnd.choice([-3 & M32, -1 & M32, 0, 0, 1, 2, 100, 340]))
        m.poke(se + 0x1C8, ATK_BASE)
        m.poke(se + 0x6C, 0x34)
    elif name in ('E3', 'E13'):
        m.poke(se + 0x88, rnd.choice([0, 1, 1, 2]))
        m.poke(se + 0xAC, rnd.choice([0, 1, 2]))
        m.poke(se + 0x6C, rnd.getrandbits(32))
    elif name == 'E28':
        pass
    m.zero(D.FX_POOL, D.N_FX * 0x100)
    fres = []
    for k in range(D.N_FX):
        a = D.FX_POOL + k * 0x100
        fres.append(a if rnd.random() > 0.08 else 0)
    return se, fres, n


def main(N=2000, seed=20261017, names=None, mut=None):
    rnd = random.Random(seed)
    m, STATS, ctx = build()
    if mut:
        mut(m)
    snapshot = (bytes(m.ram), bytes(m.init))
    m.cov.clear()
    names = names or list(H.HANDLERS)
    mism = 0
    cnt = Counter()
    first = []
    for n in range(N):
        name = rnd.choice(names)
        addr, model = H.HANDLERS[name]
        m.ram[:] = snapshot[0]
        m.init[:] = snapshot[1]
        se, fres, nent = populate(m, rnd, name)
        ram0 = bytes(m.ram)
        mem = M.Mem(ram0)
        gdt = lambda dx, dy: m.call(0x8002E814, dx & M32, dy & M32) & M32
        env = M.Env(mem, [], fres, gdt)
        model(env, se)
        ctx.j = []
        ctx.fx_results = list(fres)
        m.call(addr, se, limit=3_000_000)
        diffs = []
        if ctx.j != env.j:
            diffs.append(('journal', env.j, ctx.j))
        for (a, b) in COMPARE:
            ra = m.ram[a & 0x1FFFFFFF:b & 0x1FFFFFFF]
            ma = mem.b[a & 0x1FFFFFFF:b & 0x1FFFFFFF]
            if ra != ma:
                for k in range(len(ra)):
                    if ra[k] != ma[k]:
                        diffs.append(('mem', hex(a + k), ma[k], ra[k]))
                        break
        cnt[name] += 1
        if diffs:
            mism += 1
            if len(first) < 6:
                first.append((name, diffs[:3]))
    return mism, cnt, first, set(m.cov)


if __name__ == '__main__':
    N = int(sys.argv[1]) if len(sys.argv) > 1 else 2000
    seed = int(sys.argv[2]) if len(sys.argv) > 2 else 20261017
    names = [sys.argv[3]] if len(sys.argv) > 3 else None
    mism, cnt, first, cov = main(N, seed, names)
    print('vectors', N, 'seed', seed, 'mismatches', mism)
    for k, v in sorted(cnt.items()):
        print('  ', v, k)
    for f in first:
        print('MISMATCH', f)
    for name, (a, size) in {'E1': (0x8007ED10, 8), 'E3': (0x8007EDA0, 25), 'E13': (0x8007F30C, 27), 'E28': (0x8007FB58, 67), 'C63': (0x8007AC60, 95), 'C65': (0x8007AF20, 75),
                            'C75': (0x8007BD8C, 166)}.items():
        hit = sum(1 for x in range(a, a + 4 * size, 4) if x in cov)
        print('coverage %-4s %3d/%3d' % (name, hit, size))
    sys.exit(1 if mism else 0)
