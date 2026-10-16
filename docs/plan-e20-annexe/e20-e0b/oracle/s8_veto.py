"""s8_veto.py - where the MpMax veto applies: the creation roll (0x80032A40) stores the item whatever MpMax is ; the veto 0x80032A00 is evaluated at the DROP (0x80032B90, death)
and at the tile cut 0x80032C7C.  Real code in the espawn world (global sprite page installed, real SpawnWarpEntity behind the drop).  Output: out/veto_vectors.json."""
import sys
import json
sys.path.insert(0, 'tools')
sys.path.insert(0, '.')
import espawn as X
from machine import M32, s32

RNG = 0x80098708
E = 0x80180000                  # a hand-built dying entity (not in the entity table)


def lcg(s):
    return (s * 0x7D2B89DD + 0xE06A02E7) & M32


def find_seed(n1):
    g = 0x2468ACE1
    while True:
        g = lcg(g)
        if (lcg(g) >> 28) == n1:
            return g


w = X.World(map_no=14)
m = w.m
m.watch_uninit = False
out = []


STATS = 0x80182000


def set_mpmax(v):
    m.poke(0x80176318, STATS)       # the stats record pointer read by GetPlayerMpMax 0x8004DDDC (lh +6)
    m.poke16(STATS + 6, v)
    return s32(m.call(0x8004DDDC)) & 0xFFFF


def roll_then_drop(label, hdr_code, mpmax, nib):
    got = set_mpmax(mpmax)
    for a in range(0, 0x300, 4):
        m.poke(E + a, 0)
    hdr = 0x80181000
    for a in range(0, 0x40, 4):
        m.poke(hdr + a, 0)
    m.poke(hdr + 0x1C, hdr_code << 24)
    m.poke(E + 0x64, hdr)
    m.poke(E + 0x114, 150 << 16)
    m.poke(E + 0x118, 100 << 16)
    m.poke(E + 0x11C, 0)
    seed = find_seed(nib) if hdr_code & 0x80 else 0x12345678
    m.poke(RNG, seed)
    m.call(0x80032A40, E)
    item = s32(m.peek(E + 0x3C))
    st_roll = m.peek(RNG)
    veto = m.call(0x80032A00, item) & M32 if item else 0
    m.journal.clear()
    slots_before = [m.peek(X.ent(i) + 0x10) for i in range(8)]
    ret = m.call(0x80032B90, E) & M32
    slots_after = [m.peek(X.ent(i) + 0x10) for i in range(8)]
    sounds = [j for j in m.journal if j[1] == 'sound']
    spawned = [i for i in range(1, 8) if slots_before[i] != slots_after[i]]
    d = dict(label=label, header_code=hdr_code, mpmax=got, rng_in=seed, item_rolled=item, rng_after_roll=st_roll, veto_fn=veto, drop_return=ret, drop_sounds=[list(j[2:]) if len(j) > 2 else [] for j in sounds],
             spawned_slots=spawned, rng_after_drop=m.peek(RNG))
    if spawned:
        p = X.ent(spawned[0])
        d['spawned_id68'] = m.peek(p + 0x68)
        d['spawned_B8'] = m.peek(p + 0xB8)
        d['spawned_274'] = s32(m.peek(p + 0x274))
        d['spawned_278'] = s32(m.peek(p + 0x278))
        d['spawned_280'] = s32(m.peek(p + 0x280))
        d['spawned_284'] = m.peek(p + 0x284)
        d['spawned_flags6C'] = m.peek(p + 0x6C)
        w.wipe(p)
    out.append(d)
    print('%-62s MpMax %d rolled %3d veto %d | drop returns %d sounds %s spawned slots %s | RNG after roll %08X after drop %08X' % (label, got, item, veto, ret, d['drop_sounds'], spawned, st_roll, m.peek(RNG)))


# 0xF8 row: nibble 12..15 -> item 81 (0x51); nibble 8 -> 70 ; code 0x81 row 1: item 0x51 at index 8..., use 0x26 as a direct code too
roll_then_drop('code 0xF8 (nibble 13 -> item 81), MpMax 0', 0xF8, 0, 13)
roll_then_drop('code 0xF8 (nibble 13 -> item 81), MpMax 3', 0xF8, 3, 13)
roll_then_drop('code 0x26 direct item 38, MpMax 0', 0x26, 0, 0)
roll_then_drop('code 0x26 direct item 38, MpMax 3', 0x26, 3, 0)
roll_then_drop('code 0x52 direct item 82, MpMax 0', 0x52, 0, 0)
roll_then_drop('code 0x45 direct item 69 (not vetoed), MpMax 0', 0x45, 0, 0)
roll_then_drop('code 0 (nothing), MpMax 3', 0x00, 3, 0)
json.dump(out, open('out/veto_vectors.json', 'w'), indent=0)
