"""check.py - critic harness: runs the REAL code of ALUN_CD.EXE on my own MIPS interpreter (mymips.py) and compares, vector by vector,
with the models of the report under test (models_copy.py = a copy of e14b1/models.py with only the import line changed).
Usage: python -B check.py [N] [seed]"""
import random
import sys

import hh
from hh import Ent, Rng, M32, s32, STRIDE
import mymips
import models_copy as M

N = int(sys.argv[1]) if len(sys.argv) > 1 else 4000
SEED = int(sys.argv[2]) if len(sys.argv) > 2 else 777
OOR = len(sys.argv) > 3 and sys.argv[3] == 'oor'

ENT0 = 0x80127D30
RNG = 0x80098708
MAPPTR = 0x800E4334
MAPINFO = 0x80150000
ATTACK = 0x80151000
SELF_SLOT, TOUCH_SLOT = 6, 4

m = mymips.Mach()
J = []
state = {'hp': 0}


def h_sound(mm):
    J.append(('sound', mm.r[4] & M32))


def h_effect(mm):
    sp = mm.r[29]
    J.append(('effect', mm.r[4] & M32, mm.r[5] & M32, mm.r[6] & M32, mm.r[7] & M32, mm.rw(sp + 0x10), mm.rw(sp + 0x14)))


def h_cnh(mm):
    J.append(('cnh', ((mm.r[4] & M32) - ENT0) // STRIDE))
    mm.r[2] = state['hp']


def h_destroy(mm):
    J.append(('destroy', ((mm.r[4] & M32) - ENT0) // STRIDE, s32(mm.r[5])))


m.hooks[0x800490FC] = h_sound
m.hooks[0x8003BDD8] = h_effect
m.hooks[0x8003A374] = h_cnh
m.hooks[0x8003A59C] = h_destroy
m.ww(MAPPTR, MAPINFO)


def put(slot, e):
    a = ENT0 + slot * STRIDE
    for i, b in enumerate(e.raw):
        m.wb(a + i, b)


def get(slot):
    a = ENT0 + slot * STRIDE
    return Ent(bytes(m.rb(a + i) for i in range(STRIDE)))


gdt_cache = {}


def gdt(dx, dy):
    k = (dx & M32, dy & M32)
    if k not in gdt_cache:
        gdt_cache[k] = m.call(0x8002E814, k[0], k[1])
    return gdt_cache[k]


rnd = random.Random(SEED)


def pick(l):
    return l[rnd.randrange(len(l))]


def mk_hero():
    h = Ent()
    h.set32(M.TILEX, rnd.randrange(0, 52))
    h.set32(M.TILEY, rnd.randrange(0, 60))
    h.set32(M.POSX, rnd.randrange(0, 1 << 30))
    h.set32(M.POSY, rnd.randrange(0, 1 << 30))
    h.set32(M.FLOOR, pick([0, 0x100000, 0x200000, 0x500000, 0x700000, -0x100000]))
    h.set32(M.POSZ, h.s32(M.FLOOR))
    return h


EDGES = [0, 1, -1, 0xFFFFF, 0x100000, 0x100001, 0x180000, 0x180001, 0x1FFFFF, 0x200000, 0x200001, -0x100000, -0x200001, 0x4FFFFF, 0x500000, -0x4FFFFF, -0x500000, -0x500001]


def mk_self(hero, kind):
    e = Ent()
    e.set32(M.TYPE, pick([338, 341, 344, 392, 455, 425, 0x152, 0x155, 0x1A9, 7]) if kind == 'any' else kind)
    e.set32(M.TANIM, pick(list(range(0, 10)) + [0, 1, 3, 5, 7, 0xFFFFFFFF, 0x80000000]))
    e.set16(M.AI1, pick([0, 0, 1, 1, 2, 3, 0x14, 0x1E, 0x3C, 0x78, 0xF0, 0xFFFF, rnd.randrange(0, 0x10000)]))
    e.set16(M.AI0, pick([0, rnd.randrange(0, 0x10000), 0x7FFF, 0x8000, 0xFFFF]))
    e.set8(M.B0, pick([0, 0, 0, 1, 2, 3, 4, 9, 10, 11, 255]))
    e.set8(M.B1, pick([0, 0, 1, 2, 3, 4, 5, 255]))
    e.set8(M.B2, rnd.randrange(256))
    e.set8(M.B3, pick([0, 0, 1, 2]))
    e.set32(M.TDIR, pick([rnd.randrange(32)] * 4 + [0, 8, 15, 16, 24, 31]))
    e.set32(M.CDIR, rnd.randrange(32))
    e.set32(M.CANIM, rnd.randrange(12))
    e.set32(M.ADIR, pick([0, 1, 2, 3]) if not OOR else pick([0,1,2,3,4,0xFFFFFFFF]))
    e.set32(M.ANIMEND, pick([0, 1, 1, 0xFF, 0x80000000]))
    e.set32(M.FORCEADJ, pick([0, 1, 0, 0x100]))
    e.set32(M.ONGROUND, pick([0, 1, 1, 0]))
    e.set32(M.FLAGS, rnd.randrange(1 << 20) & ~0x40)
    e.set32(M.W28C, pick([0, 1, 6, 7, 8, 15, 16, rnd.randrange(0, 100), 0xFFFFFFFF, 0x7FFFFFFF, 0x1FFFF]))
    for o in (M.TFX, M.TFY, M.FX, M.FY, M.FSX, M.FSY):
        e.set32(o, rnd.randrange(-9999, 9999))
    e.set32(M.POSX, rnd.randrange(0, 1 << 30))
    e.set32(M.POSY, rnd.randrange(0, 1 << 30))
    hx, hy = hero.s32(M.TILEX), hero.s32(M.TILEY)
    r = rnd.random()
    if r < 0.5:
        e.set32(M.TILEX, hx + rnd.randrange(-3, 4))
        e.set32(M.TILEY, hy + rnd.randrange(-6, 4))
    elif r < 0.8:
        e.set32(M.TILEX, hx + rnd.randrange(-9, 10))
        e.set32(M.TILEY, hy + rnd.randrange(-9, 10))
    else:
        e.set32(M.TILEX, rnd.randrange(0, 52))
        e.set32(M.TILEY, rnd.randrange(0, 60))
    f = hero.s32(M.FLOOR) + pick([0, 0, 0, 0x100000, -0x100000, 0x200000, -0x300000, rnd.randrange(-0x400000, 0x400000)] + EDGES)
    e.set32(M.FLOOR, f)
    e.set32(M.POSZ, f + pick([0, 0, 0x100000, 0x200000, -0x100000, rnd.randrange(-0x600000, 0x900000)] + EDGES))
    if rnd.random() < 0.3:
        e.set32(M.POSZ, hero.s32(M.FLOOR) + pick(EDGES))
    if rnd.random() < 0.3:     # wasp: hover level relative to AI0
        t = pick(EDGES)
        ai0 = rnd.randrange(0, 0x10000)
        e.set16(M.AI0, ai0)
        e.set32(M.POSZ, (t + (ai0 << 16)) & M32)

    if kind in (338, 341, 455, 392, 344) and rnd.random() < 0.4:
        # targeted: the hero inside the entity's windows
        d = e.s32(M.ADIR)
        lat = rnd.randrange(-2, 3)
        fwd = rnd.randrange(0, 4)
        dxy = {0: (lat, -fwd), 1: (lat, fwd), 2: (fwd, lat), 3: (-fwd, lat)}.get(d, (0, 0))
        e.set32(M.TILEX, hx + dxy[0])
        e.set32(M.TILEY, hy + dxy[1])
        e.set32(M.FLOOR, hero.s32(M.FLOOR))
        if kind == 455:
            e.set32(M.POSZ, hero.s32(M.FLOOR) + pick([0, 0x100000, 0x180000, 0x1FFFFF, 0x200000, 0x200001, -0x100000]))
            e.set8(M.B0, 0)
            e.set16(M.AI1, pick([0, 0, 1, 2]))
            e.set32(M.TANIM, pick([0, 0, 0, 1, 2, 3]))
        elif kind == 338:
            e.set32(M.TANIM, pick([0, 1, 0, 1, 3]))
            e.set8(M.B0, 0)
            e.set16(M.AI1, pick([0, 0, 1]))
        elif kind == 341:
            e.set32(M.TANIM, pick([1, 1, 0]))
            e.set16(M.AI1, pick([1, 2, 5]))
        elif kind == 392:
            e.set32(M.TANIM, pick([1, 1, 0]))
            e.set32(M.FLOOR, hero.s32(M.FLOOR) + pick([0, 0x100000, 0x100001]))
        elif kind == 344:
            e.set32(M.TANIM, 1)
            e.set16(M.AI1, pick([2, 5, 9]))
            e.set32(M.FLOOR, hero.s32(M.FLOOR) + pick([0, 0x100000, 0x100001, -0x100000]))
    for o in M.MH:
        e.set32(o, f - pick([0, 0, 0, 1, 2, 3, 0x100000, 0x200000, -0x100000, -1, rnd.randrange(-0x300000, 0x600000)]))
    return e


def run_real(addr, e, hero, hp, nib, seed, mapc):
    """real code; returns (self image, hero image, rng word, journal)"""
    del J[:]
    state['hp'] = hp
    put(SELF_SLOT, e)
    put(0, hero)
    m.ww(RNG, seed)
    m.wb(MAPINFO + 0xA, mapc)
    m.ww(ATTACK - 0 + 0, 0)
    m.wb(ATTACK, nib | rnd.choice([0, 0x10, 0x20, 0x80]))
    m.ww(ENT0 + TOUCH_SLOT * STRIDE + 0x1C8, ATTACK)
    m.call(addr, ENT0 + SELF_SLOT * STRIDE)
    return get(SELF_SLOT), get(0), m.rw(RNG), list(J)


def pre(e):
    """the entity image the harness presents (TouchingEntity pointer set)"""
    e.set32(M.TOUCH, ENT0 + TOUCH_SLOT * STRIDE)
    return e


def diff(a, b):
    return [(hex(i), a.raw[i], b.raw[i]) for i in range(STRIDE) if a.raw[i] != b.raw[i]]


class Res:
    pass


results = {}


def one(name, addr, kind, runner, hpchoices=(0,), nibchoices=(0,)):
    cnt = 0
    bad = []
    ncall_cnh = 0
    cov0 = set(m.cov)
    for _ in range(N):
        hero = mk_hero()
        e = pre(mk_self(hero, kind))
        seed = rnd.randrange(1 << 32)
        mapc = rnd.randrange(256)
        hp = pick(list(hpchoices))
        nib = pick(list(nibchoices))
        # real
        er, hr, rr, jr = run_real(addr, e.copy(), hero.copy(), hp, nib, seed, mapc)
        # model
        em, hm = e.copy(), hero.copy()
        rng = Rng(seed)
        jm = []
        runner(em, hm, rng, jm, hp, nib, mapc)
        # compare: self image, hero image, rng, journal
        d1 = diff(er, em)
        d2 = diff(hr, hm)
        ok = (not d1) and (not d2) and rr == rng.s and jr == jm
        if not ok:
            bad.append((e, hero, seed, hp, nib, mapc, d1, d2, hex(rr), hex(rng.s), jr, jm))
        cnt += 1
    results[name] = (cnt, len(bad), bad[:3])
    return bad


def cnhf(hp, jm):
    def f(e):
        jm.append(('cnh', 5 - 0 if False else SELF_SLOT))
        return hp
    return f


# runners ----------------------------------------------------------------------------------------------------------------
def r_c(fn):
    def run(e, h, rng, jm, hp, nib, mapc):
        fn(e, h, rng, jm, gdt, mapc)
    return run


def r_a0(e, h, rng, jm, hp, nib, mapc):
    M.a0(e)


def r_d5(e, h, rng, jm, hp, nib, mapc):
    M.d5(e, h, rng, jm, gdt, cnhf(hp, jm), nib)


def r_d12(e, h, rng, jm, hp, nib, mapc):
    M.d12(e, h, rng, jm, gdt, cnhf(hp, jm), nib)


def r_d6(e, h, rng, jm, hp, nib, mapc):
    M.hit_common(e, h, 6, cnhf(hp, jm), gdt)


def r_d11(e, h, rng, jm, hp, nib, mapc):
    M.hit_common(e, h, 4, cnhf(hp, jm), gdt)


def main():
    covs = {}
    plan = [
        ('A0', 0x8006174C, 3, 392, r_a0, (0,), (0,)),
        ('C15', 0x80069C84, 176, 338, r_c(M.c15), (0,), (0,)),
        ('C16', 0x80069F44, 214, 341, r_c(M.c16), (0,), (0,)),
        ('C17', 0x8006A29C, 178, 344, r_c(M.c17), (0,), (0,)),
        ('C18', 0x8006A564, 260, 455, r_c(M.c18), (0,), (0,)),
        ('C19', 0x8006A974, 143, 392, r_c(M.c19), (0,), (0,)),
        ('D5', 0x8007DBE0, 62, 'any', r_d5, (0, 1), (0, 1, 2, 3, 4, 5, 6, 15)),
        ('D6', 0x8007DCD8, 25, 392, r_d6, (0, 1), (0,)),
        ('D11', 0x8007DEE8, 25, 455, r_d11, (0, 1), (0,)),
        ('D12', 0x8007DF4C, 74, 'any', r_d12, (0, 1), (0, 1, 2, 3, 4, 5, 6, 15)),
    ]
    out = []
    for name, addr, nw, kind, runner, hpc, nibc in plan:
        m.cov.clear()
        bad = one(name, addr, kind, runner, hpc, nibc)
        words = set(range(addr, addr + 4 * nw, 4))
        c = len(m.cov & words)
        covs[name] = (c, nw)
        miss = sorted(words - m.cov)
        if miss: out.append('   uncovered: ' + ' '.join(hex(x) for x in miss))
        cnt, nb, sample = results[name]
        out.append('%-4s vectors=%d mismatches=%d insn-coverage=%d/%d' % (name, cnt, nb, c, nw))
        for b in sample:
            out.append('   BAD: seed=%x hp=%d nib=%d mapc=%d diffself=%s diffhero=%s rng real=%s model=%s journal real=%s model=%s' % (b[2], b[3], b[4], b[5], b[6][:6], b[7][:3], b[8], b[9], b[10], b[11]))
    print('\n'.join(out))
    # GetDirectionToTarget sanity
    print('gdt(0,+100)=%d gdt(+100,0)=%d gdt(0,-100)=%d gdt(-100,0)=%d' % (gdt(0, 100), gdt(100, 0), gdt(0, -100), gdt(-100, 0)))


main()
