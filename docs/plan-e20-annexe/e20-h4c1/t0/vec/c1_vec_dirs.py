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


# ---- driver vectors for the base directions 4, 12, 20 and 28 (T0 (b) of h4c1; the filed set has 0, 5, 8, 16, 24, 31) ---------------------------------------------------------------------
vecs = []
ticks = list(range(20, 100, 5))
for hdir in (4, 12, 20, 28):
    for counter in ticks:
        for hx, hy, hz in ((100, 100, 1), (250, 140, 0x10001), (333, 77, 0x8000)):
            for spawn_ok in (True, False):
                reset()
                for i in range(1, 64):
                    e = M.ent(i)
                    for off in range(0, STRIDE, 4):
                        m.poke(e + off, 0)
                m.zero(D.SPAWN_POOL, D.N_SPAWN * STRIDE)
                m.poke(HERO + 0x88, 0x33)
                m.poke(HERO + 0x8C, hdir)
                m.poke(HERO + 0x114, hx << 16)
                m.poke(HERO + 0x118, hy << 16)
                m.poke(HERO + 0x11C, hz)
                m.poke(HERO + 0x18C, 0)
                m.poke(M.INUSE, 0x2B)
                m.poke(M.COUNTER, counter)
                m.poke(M.PHASE, 0)
                sres = [D.SPAWN_POOL + k * STRIDE if spawn_ok else 0 for k in range(D.N_SPAWN)]
                for k in range(D.N_SPAWN):
                    m.poke(D.SPAWN_POOL + k * STRIDE + 4, 5000 + k)
                    m.poke(D.SPAWN_POOL + k * STRIDE + 0xB8, 0x7777)
                ctx.j = []
                ctx.spawn_results = list(sres)
                ctx.fx_results = []
                ret = s32(m.call(0x8003634C, limit=3_000_000))
                spawns = [dict(a0='hero' if j[1] == HERO else hex(j[1]), a1=j[2], type=j[3], x=s32(j[4]) / 65536.0, y=s32(j[5]) / 65536.0, z=s32(j[6]), dir=j[7]) for j in ctx.j if j[0] == 'spawn']
                fzs = [s32(m.peek(D.SPAWN_POOL + k * STRIDE + 0xB8)) for k in range(len(spawns))] if spawn_ok else []
                vecs.append(dict(
                    inp=dict(phase=0, counter=counter, hero_anim=0x33, hero_dir=hdir, hero_x=hx, hero_y=hy, hero_z=hz, slope=0, spawn_returns_entity=spawn_ok),
                    out=dict(ret=ret, phase=s32(m.peek(M.PHASE)), counter=s32(m.peek(M.COUNTER)), inuse=m.peek(M.INUSE), hero_anim=s32(m.peek(HERO + 0x88)), spawns=spawns, force_z_written=fzs,
                             other_journal=[j[0] for j in ctx.j if j[0] != 'spawn'])))
json.dump(vecs, open(os.path.join(out_dir, 'vec_driver_2B_dirs.json'), 'w'))
print('driver vectors (extra directions):', len(vecs))
# independent check of the spawned directions against the table read from the binary (0x8002343C, 6 bytes per entry)
import exelib
tab = []
for i in range(32):
    t, v, d = (exelib.u8(0x8002343C + 6 * i) | exelib.u8(0x8002343C + 6 * i + 1) << 8), exelib.s16(0x8002343C + 6 * i + 2), exelib.s16(0x8002343C + 6 * i + 4)
    tab.append((t, v, d))
bad = 0
for v in vecs:
    t = v['inp']['counter']
    want = [(v['inp']['hero_dir'] + d) & 31 for (tk, vz, d) in tab if tk == t]
    got = [s['dir'] for s in v['out']['spawns']]
    if want != got:
        bad += 1
print('directions differing from (base + delta) & 31 read in the table:', bad)
