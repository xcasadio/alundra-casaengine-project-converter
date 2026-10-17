import random, sys, h2
from h2 import *
import exelib
rng = random.Random(21)
m = mk()
HERO = ENT0
PLAYER_PTR = 0x80176318; PLAYER = 0x80141000
FXBASE = 0x80150000
m.ww(PLAYER_PTR, PLAYER)
B4 = [exelib.u8(ITEMTAB + 8 * i + 4) for i in range(0x62)]
J = []
cfg = dict(fxfail=[False] * 8, n=0)

def fx(mc):
    sp = mc.r[29]
    J.append(('fx', mc.r[4] & M32, mc.r[5] & M32, mc.r[6] & M32, mc.r[7] & M32, mc.rw(sp + 0x10), mc.rw(sp + 0x14)))
    k = cfg['n']; cfg['n'] += 1
    if cfg['fxfail'][k % len(cfg['fxfail'])]:
        mc.r[2] = 0; return
    a = FXBASE + (k % 16) * 0x100
    for i in range(0x100): mc.wb(a + i, 0)
    mc.r[2] = a
m.hooks[0x8003BDD8] = fx
m.hooks[0x8004DFD8] = lambda mc: (J.append(('addmoney', s32(mc.r[4]))), setattr(mc, 'r', mc.r) or None)

def lcgstate(s): return lcg(s)

def hi(a, b): return (a * b) >> 32

def model(item, hp, hpmax, mp, mpmax, s, fxfail, hero):
    j = []; k = [0]
    def mkfx(anim):
        j.append(('fx', 0, 0xE, anim, hero[0], hero[1], (hero[2] + 0x100000) & M32))
        f = fxfail[k[0] % len(fxfail)]; k[0] += 1
        return not f
    def sparks(anim):
        nonlocal s
        out = []
        for i in range(4):
            ok = mkfx(anim)
            if ok:
                s1 = lcg(s); s2 = lcg(s1); s = s2
                out.append((0x20000, (hi(s1, 0x20001) - 0x10000) & M32, (hi(s2, 0x30001) - 0x18000) & M32))
            else: out.append(None)
        return out
    effs = []
    if item == 0x54:
        hp = min(hpmax, hp + 2)
    elif item == 0x55:
        d = max(1, (hpmax + (3 if hpmax < 0 else 0)) >> 2)
        hp = min(hpmax, hp + d)
        ok = mkfx(0)
        effs = [(0x20000,) if ok else None]
    elif item == 0x56:
        d = max(5, (hpmax + (1 if hpmax < 0 else 0)) >> 1)
        hp = min(hpmax, hp + d)
        effs = sparks(0)
    elif item in (0x51, 0x26):
        a = mp + 1
        mp = mpmax if mpmax < a else (0 if a < 0 else a)
        effs = sparks(2)
    return hp, mp, s, j, effs

bad = 0; n = 0
for it in range(6000):
    item = rng.choice([0x54, 0x55, 0x56, 0x51, 0x26])
    if item == 0x26 and False: continue
    hpmax = rng.choice([1, 3, 4, 5, 10, 16, 20, 32, 40, 100, rng.randrange(1, 200)])
    hp = rng.randrange(0, hpmax + 3)
    mpmax = rng.choice([0, 1, 2, 4]); mp = rng.randrange(0, mpmax + 1)
    s = rng.getrandbits(32); fxfail = [rng.random() < 0.3 for _ in range(rng.choice([1, 2, 5]))]
    hero = (rng.getrandbits(31), rng.getrandbits(31), rng.getrandbits(30))
    J.clear(); cfg['n'] = 0; cfg['fxfail'] = fxfail
    for i in range(0x294): m.wb(HERO + i, 0)
    m.ww(HERO + 0x14, hp); m.ww(HERO + 0x18, hpmax)
    m.ww(HERO + 0x114, hero[0]); m.ww(HERO + 0x118, hero[1]); m.ww(HERO + 0x11C, hero[2])
    m.wh(PLAYER + 4, mp); m.wh(PLAYER + 6, mpmax); m.ww(RNG, s)
    m.ww(ITEMTAB + 8 * item, 0xA0000000 + item)
    m.tracing = True
    r = m.call(0x80033F00, HERO, item)
    m.tracing = False
    real_hp = s32(m.rw(HERO + 0x14)); real_mp = s32(m.rhs(PLAYER + 4)); real_s = m.rw(RNG)
    # collect real spark forces
    realeffs = []
    for k in range(min(4, max(0, len([t for t in J if t[0] == 'fx'])))):
        a = FXBASE + (k % 16) * 0x100
        realeffs.append(a)
    mhp, mmp, ms, mj, meffs = model(item, hp, hpmax, mp, mpmax, s, fxfail, hero)
    # compare journal calls (normalise: model journal uses a0=0,a1=0xE)
    rj = [(t[0], t[1], t[2], t[3], t[4], t[5], t[6]) for t in J if t[0] == 'fx']
    mj2 = [(t[0], t[1], t[2], t[3], t[4], t[5], t[6]) for t in mj]
    # real forces per created effect, in order
    forces = []
    created = [i for i, f in enumerate([fxfail[i % len(fxfail)] for i in range(len(rj))]) if not f]
    for i in created:
        a = FXBASE + (i % 16) * 0x100
        forces.append((m.rw(a + 0x5C), m.rw(a + 0x54), m.rw(a + 0x58)))
    mforces = []
    for e_ in meffs:
        if e_ is None: continue
        if len(e_) == 1: mforces.append((e_[0], 0, 0))
        else: mforces.append(e_)
    ok = (real_hp == mhp and real_mp == mmp and real_s == ms and rj == mj2 and forces == mforces and (r & M32) == (1 if B4[item] == 0 else 0))
    n += 1
    if not ok:
        bad += 1
        if bad < 6: print('DIFF', hex(item), hp, hpmax, mp, mpmax, 'real', real_hp, real_mp, hex(real_s), 'mod', mhp, mmp, hex(ms), rj == mj2, forces == mforces, r)
print('vectors', n, 'diffs', bad)
ws = [a for a in range(0x80032E2C, 0x800330FC, 4)] + [a for a in range(0x800335AC, 0x800336F0, 4)]
print('cov (heart/herb/large heart + crystal ranges)', len([a for a in ws if a in m.cov]), len(ws))
miss = [hex(a) for a in range(0x80032E2C, 0x80033000, 4) if a not in m.cov]
print('missing in 0x80032E2C..0x80033000:', miss)
miss = [hex(a) for a in range(0x80033000, 0x800330FC, 4) if a not in m.cov]
print('missing in 0x80033000..0x800330FC:', miss)
