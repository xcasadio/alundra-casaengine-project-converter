import random
import exelib as e
from lh import *
import models as M
random.seed(7)
w = World(); m = w.m
bad = 0; n = 0
def s3(x): return x & M32
# -------- SpawnRandomLoot 0x80032C7C ---------
codes = [0xFF, 0xFE, 0xFD, 0xF8, 0x81, 0x82, 0x83, 0x80, 0x24, 0x45, 0x00, 0x62, 0x7F, 0x100, 0xF6, 0xFA, 0xFB, 0xFC, 0xF7, 0x51, 0x26, 0x52]
for it in range(12000):
    code = random.choice(codes); st = random.getrandbits(32)
    X, Y, Z = (random.getrandbits(32) for _ in range(3))
    mp = random.choice([0, 0, 1, 2, 4, -1])
    ok = random.random() < 0.9
    w.j.clear(); w.nspawn = 0; w.spawn_fail = not ok
    w.set_rng(st); w.set_mp(0, mp)
    # arguments: a0=code a1=X a2=Y a3=Z
    ret = w.call(0x80032C7C, code, X, Y, Z) & M32
    exp = M.spawn_random_loot(code, X, Y, Z, st, mp, ok)
    got_spawn = [j for j in w.j if j[0] == 'spawn']
    got_snd = [j[1] for j in w.j if j[0] == 'sound']
    fields = None
    if got_spawn and ok and ret:
        ea = FAKE_BASE
        fields = {o: m.rw(ea + o) for o in (0xB8, 0x274, 0x278, 0x27C, 0x280, 0x284)}
        fields['flags6C_clear'] = 0x80 if (m.rw(ea + 0x6C) & 0x80) == 0 and (m.rw(ea + 0x6C) | 0x80) == M32 else None
    n += 1
    gs = got_spawn[0][1:] if got_spawn else None
    if (ret != exp['ret'] or w.get_rng() != exp['state'] or gs != exp['spawn'] or got_snd != exp['sounds'] or (fields != exp['fields'] and exp['ret'])):
        bad += 1
        if bad < 8: print('MISMATCH SRL', hex(code), hex(st), mp, ok, ret, exp['ret'], gs, exp['spawn'], fields, exp['fields'], got_snd)
print('SpawnRandomLoot vectors', n, 'mismatches', bad)
fn = set(range(0x80032C7C, 0x80032D74, 4))
print(' coverage', len(w.m.cov & fn), '/', len(fn), 'missing', [hex(a) for a in sorted(fn - w.m.cov)])
w.m.cov.clear()
# -------- SpawnContents 0x80032B90 ---------
bad2 = 0; n2 = 0
items = list(range(0, 0x62)) + [0x62, 0x7F, 0x80]
for it in range(12000):
    ent = bytearray(0x294)
    item = random.choice(items); flag = random.choice([0, random.getrandbits(16), random.getrandbits(32)])
    X, Y, Z, spd = (random.getrandbits(32) for _ in range(4))
    base = ENT0 + 5 * STRIDE
    for i in range(0x294): m.wb(base + i, 0)
    m.ww(base + 0x3C, item); m.ww(base + 0x40, flag); m.ww(base + 0x114, X); m.ww(base + 0x118, Y); m.ww(base + 0x11C, Z)
    mp = random.choice([0, 0, 1, 3, 4]); ok = random.random() < 0.9
    w.j.clear(); w.nspawn = 0; w.spawn_fail = not ok; w.set_mp(0, mp)
    st = random.getrandbits(32); w.set_rng(st)
    ret = w.call(0x80032B90, base) & M32
    exp = M.spawn_contents({0x3C: item, 0x40: flag, 0x114: X, 0x118: Y, 0x11C: Z}, st, mp, ok)
    got_spawn = [j for j in w.j if j[0] == 'spawn']; got_snd = [j[1] for j in w.j if j[0] == 'sound']
    fields = None
    if got_spawn and ok and ret:
        ea = FAKE_BASE
        fields = {o: m.rw(ea + o) for o in (0xB8, 0x274, 0x278, 0x27C, 0x280, 0x284)}
        fields['flags6C_clear'] = 0x80 if (m.rw(ea + 0x6C) & 0x80) == 0 and (m.rw(ea + 0x6C) | 0x80) == M32 else None
    gs = got_spawn[0][1:] if got_spawn else None
    n2 += 1
    # the container must be untouched (+0x3C still item), rng untouched
    if (ret != exp['ret'] or gs != exp['spawn'] or got_snd != exp['sounds'] or (fields != exp['fields'] and exp['ret']) or w.get_rng() != st or m.rw(base + 0x3C) != item):
        bad2 += 1
        if bad2 < 8: print('MISMATCH SC', item, hex(flag), mp, ok, ret, exp['ret'], gs, exp['spawn'], fields, exp['fields'])
print('SpawnContents vectors', n2, 'mismatches', bad2)
fn = set(range(0x80032B90, 0x80032C7C, 4))
print(' coverage', len(w.m.cov & fn), '/', len(fn), 'missing', [hex(a) for a in sorted(fn - w.m.cov)])
