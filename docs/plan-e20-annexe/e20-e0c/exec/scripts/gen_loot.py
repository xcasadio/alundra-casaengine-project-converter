"""gen_loot.py - E20.e0c T0 (f/l1): SpawnRandomLoot 0x80032C7C and SpawnContents 0x80032B90 of ALUN_CD.EXE (France) on the REAL code (MIPS interpreter), written as JSON vectors for the
tests of the DLL (../vectors/loot-spawn.json). SpawnWarpEntity 0x80039F58 and the sound 0x800490FC are stubs that journal. Run from a COPY of this folder.
Positions: the binary passes (x, y, z) to SpawnWarpEntity; the DLL's interface takes z in its own frame (the binary's minus one, ADR-0026), so each case also carries `interfaceZ`."""
import json
import random

from lh import *
import models as M

random.seed(20261022)
w = World()
m = w.m
cases = []


def run_random(code, x, y, z, seed, mpmax, spawn_ok):
    w.j.clear()
    w.nspawn = 0
    w.spawn_fail = not spawn_ok
    w.set_rng(seed)
    w.set_mp(0, mpmax)
    ret = w.call(0x80032C7C, code, x, y, z) & M32
    return ret


def run_contents(item, flag, x, y, z, mpmax, spawn_ok):
    base = ENT0 + 5 * STRIDE
    for i in range(0x294):
        m.wb(base + i, 0)
    m.ww(base + 0x3C, item)
    m.ww(base + 0x40, flag)
    m.ww(base + 0x114, x)
    m.ww(base + 0x118, y)
    m.ww(base + 0x11C, z)
    w.j.clear()
    w.nspawn = 0
    w.spawn_fail = not spawn_ok
    w.set_mp(0, mpmax)
    w.set_rng(0x13572468)
    ret = w.call(0x80032B90, base) & M32
    return ret


def collect(ret, spawn_ok):
    spawns = [j for j in w.j if j[0] == 'spawn']
    sounds = [j[1] for j in w.j if j[0] == 'sound']
    out = dict(ret=ret, seedAfter=w.get_rng(), sounds=sounds, spawn=None, fields=None)
    if spawns:
        a = spawns[0][1:]
        out['spawn'] = dict(parent=a[0], mapPage=a[1], type=a[2], x=a[3], y=a[4], z=a[5], direction=a[6], interfaceZ=(a[5] - 1) & M32)
    if spawns and spawn_ok and ret:
        ea = FAKE_BASE
        f6c = m.rw(ea + 0x6C)
        out['fields'] = dict(forceZ=m.rw(ea + 0xB8), word274=m.rw(ea + 0x274), word278=m.rw(ea + 0x278), state27C=m.rw(ea + 0x27C), word280=m.rw(ea + 0x280),
                             word284=m.rw(ea + 0x284), collidableCleared=(f6c & 0x80) == 0 and (f6c | 0x80) == M32)
    return out


# ---- SpawnRandomLoot: a grid (every code of the tables and edge codes x 4 seeds x veto-relevant MpMax), then random ----
codes = [0x80, 0x82, 0xF7, 0xF8, 0xFA, 0xFB, 0xFC, 0xFF, 0x81, 0x83, 0xF6, 0xF9, 0xFD, 0xFE, 0x00, 0x24, 0x26, 0x45, 0x46, 0x47, 0x48, 0x51, 0x52, 0x54, 0x55, 0x56, 0x4F, 0x61, 0x62, 0x7F, 0x100]
n = 0
for code in codes:
    for seed in (0x00000000, 0x12345678, 0xB017C93D, 0xFFFFFFFF, random.getrandbits(32), random.getrandbits(32)):
        mpmax = random.choice([0, 1, 4])
        x, y, z = (random.getrandbits(32) for _ in range(3))
        spawn_ok = True
        ret = run_random(code, x, y, z, seed, mpmax, spawn_ok)
        out = collect(ret, spawn_ok)
        exp = M.spawn_random_loot(code, x, y, z, seed, mpmax, spawn_ok)
        assert (ret, w.get_rng()) == (exp['ret'], exp['state']), (hex(code), hex(seed))
        cases.append(dict(kind='random', id=f'R{n}', code=code, seed=seed, mpMax=mpmax, x=x, y=y, z=z, interfaceZ=(z - 1) & M32, spawnOk=spawn_ok, out=out))
        n += 1

# the veto: items 0x26, 0x51, 0x52 with MpMax 0 and not 0, each as a pair; the table full for the same inputs
for item in (0x26, 0x51, 0x52, 0x24, 0x45, 0x54):
    for mpmax in (0, 1):
        for spawn_ok in (True, False):
            seed = 0x2468ACE0
            x, y, z = 0x00640000, 0x00960000, 0x00100000
            ret = run_random(item, x, y, z, seed, mpmax, spawn_ok)
            cases.append(dict(kind='random', id=f'RV{n}', code=item, seed=seed, mpMax=mpmax, x=x, y=y, z=z, interfaceZ=(z - 1) & M32, spawnOk=spawn_ok, out=collect(ret, spawn_ok)))
            n += 1

# ---- SpawnContents ----
n = 0
for item in list(range(0, 0x62)) + [0x62, 0x7F, 0x80]:
    flag = random.choice([0, 0x8123, 0x0456, random.getrandbits(32)])
    mpmax = random.choice([0, 1, 4]) if item in (0x26, 0x51, 0x52) else random.choice([0, 4])
    x, y, z = (random.getrandbits(32) for _ in range(3))
    for spawn_ok in ((True, False) if item in (0x24, 0x45, 0x26, 0x51, 0x52, 0x54) else (True,)):
        ret = run_contents(item, flag, x, y, z, mpmax, spawn_ok)
        out = collect(ret, spawn_ok)
        exp = M.spawn_contents({0x3C: item, 0x40: flag, 0x114: x, 0x118: y, 0x11C: z}, 0x13572468, mpmax, spawn_ok)
        assert ret == exp['ret'], (item, flag)
        cases.append(dict(kind='contents', id=f'C{n}', item=item, flag=flag, mpMax=mpmax, x=x, y=y, z=z, interfaceZ=(z - 1) & M32, spawnOk=spawn_ok, out=out))
        n += 1
for item in (0x26, 0x51, 0x52):
    for mpmax in (0, 1):
        ret = run_contents(item, 0x77, 0x00640000, 0x00960000, 0x00100000, mpmax, True)
        cases.append(dict(kind='contents', id=f'CV{n}', item=item, flag=0x77, mpMax=mpmax, x=0x00640000, y=0x00960000, z=0x00100000, interfaceZ=0x000FFFFF, spawnOk=True, out=collect(ret, True)))
        n += 1

doc = dict(
    source='E20.e0c T0 (f/l1): SpawnRandomLoot 0x80032C7C and SpawnContents 0x80032B90 of ALUN_CD.EXE (France) on the real code, harness docs/plan-e20-annexe/e20-e0c/work/lh.py; '
           'SpawnWarpEntity 0x80039F58 and the sound 0x800490FC stubbed (journal). Seeds are the generator word BEFORE the call (SpawnContents does not draw; its seed is 0x13572468).',
    cases=cases)
json.dump(doc, open('loot-spawn.json', 'w'), indent=1)
import collections
print('cases', len(cases), collections.Counter((c['kind'], c['out']['ret']) for c in cases))
