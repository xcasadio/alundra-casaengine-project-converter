import random, sys, collections, pickle
from lh import *
import models as M
import give_model as G
random.seed(int(sys.argv[1]) if len(sys.argv) > 1 else 5)
w = World(); m = w.m
INV = 0x80142000; NITEMS = 0x60; FX = 0x80144000
m.ww(0x8017638C, NITEMS); m.ww(0x8017631C, INV)
hero = ENT0
class Fx:
    full = False; n = 0
fxj = []
def effect_stub(mc):
    sp = mc.r[29]
    fxj.append(('effect', mc.r[4] & M32, mc.r[5] & M32, mc.r[6] & M32, mc.r[7] & M32, mc.rw(sp + 0x10), mc.rw(sp + 0x14)))
    if Fx.full: mc.r[2] = 0
    else:
        a = FX + Fx.n * 0x100; Fx.n += 1
        for i in range(0x100): mc.wb(a + i, 0)
        mc.r[2] = a
m.hooks[0x8003BDD8] = effect_stub
bad = 0; n = 0
items = [0x24, 0x45, 0x46, 0x47, 0x48, 0x4F, 0x51, 0x54, 0x55, 0x56]
REC = []
for it in range(30000):
    item = random.choice(items)
    st = dict(hp=random.randrange(0, 60), hpmax=random.choice([4, 5, 7, 8, 12, 16, 20, 21, 23, 40, 50, 1, 2, 3]),
              money=random.choice([0, 1, 9990, 9994, 9999, random.randrange(0, 10000)]), mp=random.randrange(0, 5), mpmax=random.randrange(0, 5),
              pending=random.choice([0, 5, 0x31, 0x32, 0x33, random.randrange(0, 60)]), inv={}, invmax={}, rng=random.getrandbits(32), j=[],
              fx_full=random.random() < 0.15, hx=random.getrandbits(32), hy=random.getrandbits(32), hz=random.getrandbits(32))
    st['hp'] = min(st['hp'], st['hpmax']) if random.random() < 0.8 else st['hp']
    mxs = {i: M.s32(0) for i in items}
    cnt = random.choice([0, 1, 8, 9, 5])
    st['inv'] = {0x24: cnt}; st['invmax'] = {0x24: 9}
    # real memory
    for i in range(NITEMS): m.wh(INV + 4 * i + 2, 0); m.wh(INV + 4 * i, 0)
    m.wh(INV + 4 * 0x24 + 2, cnt)
    base = PLAYER
    m.wh(base + 4, st['mp']); m.wh(base + 6, st['mpmax']); m.wh(base + 8, st['money'] & 0xFFFF); m.wh(base + 0xE, st['pending'])
    m.ww(hero + 0x14, st['hp'] & M32); m.ww(hero + 0x18, st['hpmax']); m.ww(hero + 0x114, st['hx']); m.ww(hero + 0x118, st['hy']); m.ww(hero + 0x11C, st['hz'])
    m.ww(0x801EB3F8, 0)
    w.set_rng(st['rng']); fxj.clear(); Fx.full = st['fx_full']; Fx.n = 0
    ret = w.call(0x80033F00, hero, item) & M32
    # model
    st2 = dict(st); st2['inv'] = dict(st['inv']); st2['j'] = []
    rm = G.give(item, st2)
    got = dict(hp=M.s32(m.rw(hero + 0x14)), mp=M.s32(m.rhs(base + 4)), money=m.rhs(base + 8), pending=m.rh(base + 0xE), rng=w.get_rng(), cnt=m.rhs(INV + 4 * 0x24 + 2), bit=bool(m.rw(0x801EB3F8) & 0x400))
    exp = dict(hp=M.s32(st2['hp']), mp=st2['mp'], money=st2['money'], pending=st2['pending'] & 0xFFFF, rng=st2['rng'], cnt=st2['inv'].get(0x24, 0), bit=st2.get('bit400', False))
    # effect journal and velocities
    fj_real = list(fxj)
    fj_model = [x for x in st2['j'] if x[0] == 'effect']
    vel_real = []
    for k in range(Fx.n):
        a = FX + k * 0x100
        vel_real.append((m.rw(a + 0x5C), m.rw(a + 0x54), m.rw(a + 0x58)))
    vel_model = []
    cur = None
    for x in st2['j']:
        if x[0] == 'fx5c': vel_model.append((0x20000, 0, 0))
        elif x[0] == 'fxvel': vel_model.append((x[1], x[2], x[3]))
    n += 1
    if (ret != rm or got != exp or [tuple(x) for x in fj_real] != [tuple(x) for x in fj_model] or vel_real != vel_model):
        bad += 1
        if bad < 6: print('MISMATCH give', hex(item), st['hpmax'], ret, rm, got, exp, fj_real[:2], fj_model[:2], vel_real[:2], vel_model[:2])
    REC.append((item, st, ret))
print('GiveItem vectors', n, 'mismatches', bad)
fn = set(range(0x80033F00, 0x80034100, 4))
cov = m.cov & fn
print(' coverage of 0x80033F00 itself', len(cov), '/', len(fn))
for name, lo, hi in [('0x80032E2C..0x80033100 hearts+herb', 0x80032E2C, 0x80033100), ('0x800335AC crystal', 0x800335AC, 0x800336F0), ('0x80033EC8 helper', 0x80033EC8, 0x80033F00), ('0x8004DF10..', 0x8004DF80, 0x8004E030)]:
    f = set(range(lo, hi, 4)); print(' ', name, len(m.cov & f), '/', len(f))
