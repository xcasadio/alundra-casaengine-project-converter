"""driver_keys.py - h4c2 discovery: cases of the earth-2 driver (item 0x2C, wrapper 0x8003634C -> driver 0x800354D0 -> guard 0x800351B8 -> cancel 0x80034BDC) run on the REAL code
(the machine of h4_vec_drivers.py, spawn / effect / sound stubbed and journalled), with a controlled hero, generator state and spawn result.  Output: JSON, one object per case.
Every value is an unsigned 32-bit word unless named otherwise; positions are 16.16; the hero Z is the RAW binary Z (the DLL's PosZ + 1).
usage: python -B driver_keys.py OUT.json"""
import sys
import json
import random
import h4_vec_drivers as D
import h4_models as M
from machine import s32, M32

HERO = M.HERO
STRIDE = M.STRIDE
TICKS = [16, 32, 48, 64, 80, 96, 112, 128, 144, 160, 176, 192, 224, 240]


def case(m, ctx, snap, phase, counter, anim, slope, hx, hy, hz, seed, spawns, term, others=()):
    """spawns: list of 'nul' / 'ok' for the successive spawn results; term: the +0x138 given to every returned entity ('lt': hz+0x1000001, 'eq': hz+0x1000000, 'lo': 0)"""
    m.ram[:] = snap[0]
    m.init[:] = snap[1]
    m.journal.clear()
    for i in range(1, 64):
        for off in range(0, STRIDE, 4):
            m.poke(M.ent(i) + off, 0)
    m.poke(HERO + 0x88, anim)
    m.poke(HERO + 0x8C, 24)
    m.poke(HERO + 0x114, hx)
    m.poke(HERO + 0x118, hy)
    m.poke(HERO + 0x11C, hz)
    m.poke(HERO + 0x18C, slope)
    m.poke(M.INUSE, 0x2C)
    m.poke(M.COUNTER, counter)
    m.poke(M.PHASE, phase)
    m.poke(M.RNG, seed)
    m.poke(0x80134360, 0)
    # other entities (for the cancel): list of (slot, status, type)
    top = 0
    for (slot, status, typ) in others:
        e = M.ent(slot)
        m.poke(e + 0x10, status)
        m.poke(e + 0x68, typ)
        m.poke(e + 0x20, 5)
        m.poke(e + 0x88, 0)
        top = max(top, slot)
    m.poke(0x80134360, top)
    m.zero(D.SPAWN_POOL, D.N_SPAWN * STRIDE)
    res = []
    tval = {'lt': (hz + 0x1000001) & M32, 'eq': (hz + 0x1000000) & M32, 'lo': 0}[term]
    for k in range(D.N_SPAWN):
        a = D.SPAWN_POOL + k * STRIDE
        m.poke(a + 4, 5000 + k)
        m.poke(a + 0x138, tval)
        m.poke(a + 0x10, 1)
    for k, s in enumerate(spawns):
        res.append(D.SPAWN_POOL + k * STRIDE if s == 'ok' else 0)
    ctx.j = []
    ctx.spawn_results = list(res)
    ctx.fx_results = []
    ret = s32(m.call(0x8003634C, limit=3_000_000))
    out = dict(inputs=dict(phase=phase, counter=counter, hero_anim=anim, slope=slope, hero_x=hx, hero_y=hy, hero_z_raw=hz, seed=seed, spawns=spawns, terrain=term,
                           others=[list(o) for o in others]),
               ret=ret, journal=[list(j) for j in ctx.j], rng_after=m.peek(M.RNG) & M32, phase_after=m.peeks(M.PHASE), counter_after=m.peeks(M.COUNTER),
               item_after=m.peeks(M.INUSE), hero_anim_after=m.peeks(HERO + 0x88),
               pool=[dict(status=m.peeks(D.SPAWN_POOL + k * STRIDE + 0x10), force_z=m.peek(D.SPAWN_POOL + k * STRIDE + 0xB8) & M32) for k in range(len(spawns))],
               others_after=[dict(slot=o[0], status=m.peeks(M.ent(o[0]) + 0x10), b20=m.peeks(M.ent(o[0]) + 0x20), anim=m.peeks(M.ent(o[0]) + 0x88)) for o in others])
    return out


def main():
    rnd = random.Random(20261018)
    m, STATS, ctx = D.build_machine()
    snap = (bytes(m.ram), bytes(m.init))
    cases = []
    seeds = [0x12345678, 0xB017C93D, 0, 1, 0xFFFFFFFF, 0xDEADBEEF] + [rnd.getrandbits(32) for _ in range(6)]
    heroes = [(100 << 16, 100 << 16, 1), (600 << 16, 480 << 16, 0x300001), (51 << 16 | 0x8000, 233 << 16 | 0x4000, 0x48001)]
    # phase 0: every tick of the table, its neighbours, the tick where the list ends; one and two spawns (224 has two entries)
    counters = sorted(set(TICKS + [t - 1 for t in TICKS] + [t + 1 for t in TICKS] + [0, 1, 15, 17, 241, 250]))
    for seed in seeds[:6]:
        for (hx, hy, hz) in heroes[:2]:
            for c in counters:
                n = 2 if c == 224 else 1
                cases.append(case(m, ctx, snap, 0, c, 0x33, 1, hx, hy, hz, seed, ['ok'] * n, 'lo'))
    # the terrain test: lt (destroyed), eq (kept), lo (kept), a refused spawn
    for term in ('lt', 'eq', 'lo'):
        cases.append(case(m, ctx, snap, 0, 16, 0x33, 0, *heroes[0], seeds[0], ['ok'], term))
        cases.append(case(m, ctx, snap, 0, 224, 0x32, 0, *heroes[1], seeds[1], ['ok', 'ok'], term))
    cases.append(case(m, ctx, snap, 0, 16, 0x33, 0, *heroes[0], seeds[0], ['nul'], 'lo'))
    cases.append(case(m, ctx, snap, 0, 224, 0x33, 0, *heroes[0], seeds[0], ['nul', 'ok'], 'lt'))
    cases.append(case(m, ctx, snap, 0, 224, 0x33, 0, *heroes[0], seeds[0], ['ok', 'nul'], 'lo'))
    # the phase change: the last entry (240) sets the phase to 1 in the same call; one more tick; the entries do not repeat
    for c in (240, 241, 242):
        cases.append(case(m, ctx, snap, 0, c, 0x33, 0, *heroes[0], seeds[0], ['ok'], 'lo'))
    # phase 1
    for c in (241, 0x116, 0x117, 0x118, 0x119, 0x200):
        for anim in (0x33, 0x32, 0x34, 0x31, 0):
            cases.append(case(m, ctx, snap, 1, c, anim, 0, *heroes[0], seeds[0], [], 'lo'))
        cases.append(case(m, ctx, snap, 1, c, 0x33, 4, *heroes[0], seeds[0], [], 'lo'))
    # phase 2
    for anim in (0x34, 0x33, 0x32, 0, 0x2D):
        cases.append(case(m, ctx, snap, 2, 300, anim, 0, *heroes[0], seeds[0], [], 'lo'))
        cases.append(case(m, ctx, snap, 2, 300, anim, 4, *heroes[0], seeds[0], [], 'lo'))
    # the guard in phase 0, with rocks in the world (the cancel): slope 4, a foreign animation; rocks at status 2 / 3 / 1, a foreign type at status 2, a rock in the highest slot
    others = [(1, 2, 0x11), (2, 3, 0x11), (3, 2, 0x13), (4, 2, 0x11), (5, 1, 0x11), (6, 2, 0x11)]
    for (anim, slope) in ((0x31, 0), (0x33, 4), (0x32, 4), (0, 0), (0x34, 0)):
        for ph in (0, 1):
            cases.append(case(m, ctx, snap, ph, 100, anim, slope, *heroes[0], seeds[0], ['ok'], 'lo', others))
    # the guard does not run in phase 2
    cases.append(case(m, ctx, snap, 2, 100, 0x33, 4, *heroes[0], seeds[0], [], 'lo', others))
    json.dump(cases, open(sys.argv[1], 'w'))
    bad = [c for c in cases if c['ret'] not in (0, 1)]
    print('cases', len(cases), 'ret histogram', {r: sum(1 for c in cases if c['ret'] == r) for r in (0, 1)}, 'non 0/1', len(bad))


main()
