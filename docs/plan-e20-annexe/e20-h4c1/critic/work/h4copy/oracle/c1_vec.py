"""c1_vec.py (h4c1 discovery, own script on a COPY of the e20-h4 harness): input/output vectors of the REAL earth-1 driver (wrapper 0x8003634C, item 0x2B -> 0x80035320, slope guard 0x800351B8,
neutral cancel 0x800351A0) and of the REAL E3 (0x8007EDA0), a deterministic grid, written as JSON for the DLL tests.  SpawnWarpEntity 0x80039F58 is the journalling stub of the e20-h4 vector machine
(returns a pool entity or 0).  Nothing here knows an expected value of the DLL.
usage: python -B c1_vec.py OUTDIR"""
import itertools
import json
import os
import sys
import random
import spellworld as S
import world as W
from machine import s32, M32
import h4_models as M
import h4_vec_drivers as D

HERO = M.HERO
STRIDE = M.STRIDE
out_dir = sys.argv[1]
os.makedirs(out_dir, exist_ok=True)

m, STATS, ctx = D.build_machine()
m.cov.clear()
destroy_journal = []
def destroy(mm):
    destroy_journal.append(('destroy', mm.r[4] & M32, mm.r[5] & M32))
    mm.r[2] = 0
m.hooks[0x8003A59C] = destroy
snapshot = (bytes(m.ram), bytes(m.init))


def reset():
    m.ram[:] = snapshot[0]
    m.init[:] = snapshot[1]
    m.journal.clear()


# ---- driver vectors ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------
counters = list(range(0, 31)) + [31, 32, 33, 40, 50, 60, 70, 80, 90, 93, 94, 95, 96, 97, 98, 99, 100, 101, 102, 150, 0x7FFFFFFD, 0x7FFFFFFE, 0x7FFFFFFF]
vecs = []
rnd = random.Random(20261018)
for phase in (0, 1, 2):
    for counter in counters:
        if phase == 0 and counter > 0xFFF:
            continue                      # phase 0 is unreachable past the table (counter <= 95 by construction); beyond 0xFFFF the table loop would read past its sentinel
        for anim in (0x33, 0x32, 0x34, 0x31, 0x00):
            for slope in (0, 4):
                for spawn_ok in (True, False):
                    if spawn_ok is False and counter not in (20, 25, 95, 21):
                        continue
                    if slope == 4 and anim != 0x33 and counter not in (20, 99, 100):
                        continue
                    if anim in (0x31, 0x00) and counter not in (0, 19, 20, 25, 95, 99, 100, 101):
                        continue
                    if anim == 0x34 and phase != 2 and counter not in (20, 100):
                        continue
                    reset()
                    # world: a zeroed pool, the hero
                    for i in range(1, 64):
                        e = M.ent(i)
                        for off in range(0, STRIDE, 4):
                            m.poke(e + off, 0)
                    m.zero(D.SPAWN_POOL, D.N_SPAWN * STRIDE)
                    hdir = rnd.choice([24, 24, 24, 0, 8, 16, 5, 31]) if counter in (20, 25, 30, 95) else 24
                    hx, hy, hz = rnd.choice([(100, 100, 1), (250, 140, 0x10001), (333, 77, 0x8000)])
                    m.poke(HERO + 0x88, anim)
                    m.poke(HERO + 0x8C, hdir)
                    m.poke(HERO + 0x114, hx << 16)
                    m.poke(HERO + 0x118, hy << 16)
                    m.poke(HERO + 0x11C, hz)
                    m.poke(HERO + 0x18C, slope)
                    m.poke(M.INUSE, 0x2B)
                    m.poke(M.COUNTER, counter)
                    m.poke(M.PHASE, phase)
                    sres = [D.SPAWN_POOL + k * STRIDE if spawn_ok else 0 for k in range(D.N_SPAWN)]
                    for k in range(D.N_SPAWN):
                        m.poke(D.SPAWN_POOL + k * STRIDE + 4, 5000 + k)
                        m.poke(D.SPAWN_POOL + k * STRIDE + 0xB8, 0x7777)        # a sentinel in ForceZ: a write shows
                    ctx.j = []
                    ctx.spawn_results = list(sres)
                    ctx.fx_results = []
                    ret = s32(m.call(0x8003634C, limit=3_000_000))
                    spawns = [dict(a0='hero' if j[1] == HERO else hex(j[1]), a1=j[2], type=j[3], x=s32(j[4]) / 65536.0, y=s32(j[5]) / 65536.0, z=s32(j[6]), dir=j[7]) for j in ctx.j if j[0] == 'spawn']
                    fzs = [s32(m.peek(D.SPAWN_POOL + k * STRIDE + 0xB8)) for k in range(len(spawns))] if spawn_ok else []
                    vecs.append(dict(
                        inp=dict(phase=phase, counter=counter, hero_anim=anim, hero_dir=hdir, hero_x=hx, hero_y=hy, hero_z=hz, slope=slope, spawn_returns_entity=spawn_ok),
                        out=dict(ret=ret, phase=s32(m.peek(M.PHASE)), counter=s32(m.peek(M.COUNTER)), inuse=m.peek(M.INUSE), hero_anim=s32(m.peek(HERO + 0x88)), spawns=spawns, force_z_written=fzs,
                                 other_journal=[j[0] for j in ctx.j if j[0] != 'spawn'])))
json.dump(vecs, open(os.path.join(out_dir, 'vec_driver_2B.json'), 'w'))
print('driver vectors:', len(vecs))

# ---- E3 vectors --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------
ev = []
rnd = random.Random(20261019)
flags_list = [0x132114, 0x132154, 0x132144, 0x0, 0xFFFFFFFF, 0x30, 0x40, 0x10, 0x20, 0x100000, 0x132114 | 0x80, 0x8000FFFF, rnd.getrandbits(32), rnd.getrandbits(32)]
for ta, ae, fl in itertools.product((0, 1, 2, 5), (0, 1, 2, 0xFFFFFFFF), flags_list):
    reset()
    destroy_journal.clear()
    se = M.ent(3)
    for off in range(0, STRIDE, 4):
        m.poke(se + off, 0)
    m.poke(se + 0x10, 3)
    m.poke(se + 0x68, 16)
    m.poke(se + 0x88, ta)
    m.poke(se + 0xAC, ae)
    m.poke(se + 0x6C, fl)
    m.call(0x8007EDA0, se, limit=1_000_000)
    ev.append(dict(inp=dict(target_anim=ta, anim_end=ae if ae < 0x80000000 else -1, flags=fl), out=dict(target_anim=s32(m.peek(se + 0x88)), flags=m.peek(se + 0x6C), anim_end=s32(m.peek(se + 0xAC)),
                                                                                                     destroy=[(d[0], 'self' if d[1] == se else hex(d[1]), s32(d[2])) for d in destroy_journal])))
json.dump(ev, open(os.path.join(out_dir, 'vec_e3.json'), 'w'))
print('E3 vectors:', len(ev))
def coverage(lo, hi):
    pcs = set(range(lo, hi, 4))
    return len(pcs & set(m.cov)), len(pcs)
print('coverage 0x80035320 driver (108 words):', coverage(0x80035320, 0x800354D0))
print('coverage 0x800351B8 guard:', coverage(0x800351B8, 0x80035204))
print('coverage 0x800350C0 cancel:', coverage(0x800350C0, 0x800351B8), '(only the neutral entry of 0x2B is reachable here)')
print('coverage 0x8003634C wrapper:', coverage(0x8003634C, 0x800364B8))
print('coverage E3 0x8007EDA0:', coverage(0x8007EDA0, 0x8007EE04))
miss = sorted(set(range(0x80035320, 0x800354D0, 4)) - set(m.cov))
print('driver words never executed:', [hex(a) for a in miss])
