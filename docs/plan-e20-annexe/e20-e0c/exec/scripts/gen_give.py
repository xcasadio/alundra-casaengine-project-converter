"""gen_give.py - E20.e0c T0 (g): GiveItem 0x80033F00 of ALUN_CD.EXE (France) on the REAL code (MIPS interpreter), for every object a loot drop or a placed object can hand
to the hero, plus control items; writes ../vectors/give-item.json. Run from a COPY of this folder (reads the binary at the path exelib names, writes next to itself).
The creator of effects 0x8003BDD8 and the sound 0x800490FC are stubs that journal; the stub of the creator writes the position (x, y, z) to +0x3C/+0x40/+0x44 of a zeroed record
before the real code that follows (rings, sparks) writes its own offsets and forces (+0x54/+0x58/+0x5C)."""
import json
import random
import sys

from lh import *

random.seed(20261022)
w = World()
m = w.m
INV = 0x80142000
NITEMS = 0x60
FX = 0x80144000
m.ww(0x8017638C, NITEMS)
m.ww(0x8017631C, INV)
hero = ENT0
BASE = PLAYER


class Pool:
    free = None        # None = every creation succeeds, else the number of creations that still succeed
    n = 0


fxj = []


def effect_stub(mc):
    sp = mc.r[29]
    args = (mc.r[4] & M32, mc.r[5] & M32, mc.r[6] & M32, mc.r[7] & M32, mc.rw(sp + 0x10), mc.rw(sp + 0x14))
    if Pool.free is not None and Pool.free <= 0:
        fxj.append({'args': list(args), 'addr': 0})
        mc.r[2] = 0
        return
    if Pool.free is not None:
        Pool.free -= 1
    a = FX + Pool.n * 0x100
    Pool.n += 1
    for i in range(0x100):
        mc.wb(a + i, 0)
    mc.ww(a + 0x3C, args[3])
    mc.ww(a + 0x40, args[4])
    mc.ww(a + 0x44, args[5])
    fxj.append({'args': list(args), 'addr': a})
    mc.r[2] = a


m.hooks[0x8003BDD8] = effect_stub

ITEMS = [0x24, 0x25, 0x26, 0x27, 0x29, 0x45, 0x46, 0x47, 0x48, 0x4F, 0x51, 0x52, 0x53, 0x54, 0x55, 0x56, 0x04, 0x21, 0x3D]
HEROES = [(0, 0, 0), (0x00640000, 0x00960000, 0x00100000), (0x7FFF0000, 0x80010000, 0xFFF00000), (0x123456, 0x789ABC, 0x3C0001)]


def s32l(x):
    return s32(x)


def inv_max(item):
    # the maximum of g_itemsProperties column 3 is read by the real AddOneItemIfUnlocked; probe it: add until the count stops
    m.wh(INV + 4 * item + 2, 0)
    n = 0
    while n < 120:
        before = m.rhs(INV + 4 * item + 2)
        w.call(0x8004E530, item)
        after = m.rhs(INV + 4 * item + 2)
        if after == before:
            return before
        n += 1
    return -1


MAXES = {it: inv_max(it) for it in ITEMS}

vectors = []
for item in ITEMS:
    for k in range(12):
        mx = MAXES[item]
        before = random.choice([0, mx, mx, max(0, mx - 1), 0])
        hpmax = random.choice([1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 12, 16, 20, 21, 23, 40, 50])
        hp = random.choice([0, 1, hpmax, hpmax, max(0, hpmax - 1), random.randrange(0, hpmax + 1)])
        mpmax = random.choice([0, 1, 2, 3, 4])
        mp = random.choice([0, mpmax, random.randrange(0, mpmax + 1)])
        money = random.choice([0, 1, 9990, 9994, 9999, random.randrange(0, 10000)])
        pending = random.choice([0, 5, 0x31, 0x32, random.randrange(0, 60)])
        seed = random.getrandbits(32)
        pos = random.choice(HEROES)
        free = random.choice([None, None, None, 0, 1, 3, 5, 9])
        state = dict(item=item, count=before, hp=hp, hpmax=hpmax, mp=mp, mpmax=mpmax, money=money, pending=pending, seed=seed, hero=list(pos), free=free)
        for i in range(NITEMS):
            m.wh(INV + 4 * i, 0)
            m.wh(INV + 4 * i + 2, 0)
        m.wh(INV + 4 * item + 2, before)
        m.wh(BASE + 4, mp)
        m.wh(BASE + 6, mpmax)
        m.wh(BASE + 8, money & 0xFFFF)
        m.wh(BASE + 0xE, pending)
        m.ww(hero + 0x14, hp & M32)
        m.ww(hero + 0x18, hpmax)
        m.ww(hero + 0x114, pos[0])
        m.ww(hero + 0x118, pos[1])
        m.ww(hero + 0x11C, pos[2])
        m.ww(0x801EB3F8, 0)
        w.set_rng(seed)
        w.j.clear()
        fxj.clear()
        Pool.free = free
        Pool.n = 0
        ret = w.call(0x80033F00, hero, item) & M32
        effects = []
        for e in fxj:
            rec = {'anim': e['args'][2], 'x': e['args'][3], 'y': e['args'][4], 'z': e['args'][5], 'created': e['addr'] != 0}
            if e['addr']:
                a = e['addr']
                rec.update(x3c=m.rw(a + 0x3C), y40=m.rw(a + 0x40), z44=m.rw(a + 0x44), fx=m.rw(a + 0x54), fy=m.rw(a + 0x58), fz=m.rw(a + 0x5C))
            effects.append(rec)
        out = dict(
            ret=ret, hp=s32(m.rw(hero + 0x14)), hpmax=s32(m.rw(hero + 0x18)), mp=m.rhs(BASE + 4), money=m.rhs(BASE + 8), pending=m.rh(BASE + 0xE),
            count=m.rhs(INV + 4 * item + 2), seed=w.get_rng(), flag1450=bool(m.rw(0x801EB3F8) & 0x400),
            sounds=[j[1] for j in w.j if j[0] == 'sound'], effects=effects)
        vectors.append(dict(state=state, out=out))

doc = dict(
    source='E20.e0c T0 (g): GiveItem 0x80033F00 of ALUN_CD.EXE (France) on the real code, harness docs/plan-e20-annexe/e20-e0c/work/lh.py; effect creator and sound stubbed (journal)',
    inventoryMaxima={hex(k): v for k, v in MAXES.items()}, vectors=vectors)
json.dump(doc, open('give-item.json', 'w'), indent=1)
print('vectors', len(vectors), 'maxima', {hex(k): v for k, v in MAXES.items()})
import collections
c = collections.Counter((v['state']['item'], len(v['out']['effects'])) for v in vectors)
print(sorted((hex(k[0]), k[1], n) for k, n in c.items())[:80])
