"""my_query.py - checker's OWN model of the target query 0x8003AF70 and of the sort 0x8003AE04, written from the disassembly I read
(not from h4_models.py), compared with the REAL code in the copied machine on random entity tables.
usage: python -B my_query.py [N] [seed]"""
import random
import sys
import spellworld as S
import world as W
from machine import s32, M32

HERO = 0x80127D30
STRIDE = 0x294
COUNT = 0x80134360
PTRS = 0x801E0000
DIST = 0x801E0200
SHEET = 0x801E7000


def ent(i):
    return HERO + STRIDE * i


def u32(x):
    return x & M32


def my_sort(keys, ptrs, lo, hi):
    """Hoare variant read at 0x8003AE04: pivot = (key[lo] + key[hi]) >> 1 (unsigned 32-bit sum), crossing scans, swap, recurse (lo, i-1) and (j+1, hi)."""
    i, j = lo, hi
    pivot = (u32(keys[hi] + keys[lo])) >> 1
    while True:
        while u32(keys[i]) < pivot:
            i += 1
        while pivot < u32(keys[j]):
            j -= 1
        if i < j:
            keys[i], keys[j] = keys[j], keys[i]
            ptrs[i], ptrs[j] = ptrs[j], ptrs[i]
            i += 1
            j -= 1
            continue
        break
    i -= 1
    if lo < i:
        my_sort(keys, ptrs, lo, i)
    if j + 1 < hi:
        my_sort(keys, ptrs, j + 1, hi)


def my_query(m, caller, mask, attr, count):
    keys, ptrs = [], []
    for i in range(1, count + 1):                       # slot 1 .. count-1 : the slot `count` is NOT visited
        e = ent(i)
        if e == caller:
            continue
        if u32(m.peek(e + 0x10) - 2) >= 2:          # status 2 or 3
            continue
        if m.peek(e + 0x20) != 0:
            continue
        if (m.peek(e + 0x6C) & mask) == 0:
            continue
        if m.peek(e + 0x1D0) != 0 or m.peek(e + 0x1CC) != 0:
            continue
        if m.peek(e + 0xB4) & 0x40:
            continue
        rec = m.peek(e + 0x1C4)
        if rec == 0:
            continue
        if (m.rb(rec + attr + 2) & 0xC0) == 0x80:
            continue
        dx = s32(u32(m.peek(e + 0x114) - m.peek(caller + 0x114))) >> 16
        dy = s32(u32(m.peek(e + 0x118) - m.peek(caller + 0x118))) >> 16
        keys.append(u32(dx * dx + dy * dy))
        ptrs.append(e)
    if len(keys) >= 3:
        my_sort(keys, ptrs, 0, len(keys) - 1)
    return keys, ptrs


def main(N, seed):
    rnd = random.Random(seed)
    m, STATS, placed = S.make(item=None, indices=(1,))
    mism = 0
    covered = set()
    m.cov.clear()
    ties = 0
    for n in range(N):
        for b in range(0, 16 * 0x20):
            m.poke8(SHEET + b, 0)
        for k in range(16):
            for t in range(0, 14):
                m.poke8(SHEET + 0x20 * k + 2 + t, rnd.choice([0x10, 0x10, 0x20, 0x80, 0xC0, 0x40, 0x00, 0x8F, 0x90]))
        cnt = rnd.choice([0, 1, 2, 3, 4, 5, 7, 9, 12, 20])
        m.poke(COUNT, cnt)
        caller = ent(rnd.choice([0, 0, rnd.randrange(0, 64)]))
        hx, hy = rnd.randrange(100 << 16, 300 << 16), rnd.randrange(100 << 16, 300 << 16)
        m.poke(caller + 0x114, hx)
        m.poke(caller + 0x118, hy)
        for i in range(1, 64):
            e = ent(i)
            if e == caller:
                continue
            m.poke(e + 0x10, rnd.choice([2, 2, 2, 3, 3, 0, 1, 4]))
            m.poke(e + 0x20, 0 if rnd.random() < 0.9 else 7)
            m.poke(e + 0x6C, rnd.choice([1, 1, 1, 0, 5, 9, 0x101]))
            m.poke(e + 0x1D0, 0 if rnd.random() < 0.9 else 1)
            m.poke(e + 0x1CC, 0 if rnd.random() < 0.9 else 1)
            m.poke(e + 0xB4, 0 if rnd.random() < 0.9 else 0x40)
            m.poke(e + 0x1C4, 0 if rnd.random() < 0.05 else SHEET + 0x20 * rnd.randrange(16))
            # coarse positions: many equal distances (ties) to expose the instability of the sort
            ox = rnd.choice([0, 8, 16, 24, 32, -8, -16, 40, 64, 160, 161, -200])
            oy = rnd.choice([0, 8, 16, 24, 32, -8, -16, 40, 64, 0, 1])
            m.poke(e + 0x114, (hx + (ox << 16) + rnd.choice([0, 0, 0, 0x4000])) & M32)
            m.poke(e + 0x118, (hy + (oy << 16)) & M32)
        mask = rnd.choice([1, 1, 1, 4, 5])
        attr = rnd.choice([8, 9, 10, 11, 11, 0, 3])
        keys, ptrs = my_query(m, caller, mask, attr, cnt)
        # real code
        for k in range(64):
            m.poke(PTRS + 4 * k, 0xDEAD0000 + k)
            m.poke(DIST + 4 * k, 0xBEEF0000 + k)
        m.poke(0x801FFF10, DIST)
        ret = s32(m.call(0x8003AF70, caller, mask, attr, PTRS))
        rptrs = [m.peek(PTRS + 4 * k) for k in range(max(ret, 0))]
        rkeys = [m.peek(DIST + 4 * k) for k in range(max(ret, 0))]
        if len(set(keys)) < len(keys):
            ties += 1
        if ret != len(keys) or rptrs != ptrs or rkeys != keys:
            mism += 1
            if mism <= 5:
                print('MISMATCH vec', n, 'count', cnt, 'real', ret, [hex(x) for x in rptrs][:6], rkeys[:6], 'model', len(keys), [hex(x) for x in ptrs][:6], keys[:6])
    # coverage of the two functions
    def cov(a, b):
        return len([p for p in range(a, b, 4) if p in m.cov]), (b - a) // 4
    print('vectors', N, 'seed', seed, 'mismatches', mism, 'vectors with tied keys', ties)
    print('coverage query 0x8003AF70..0x8003B0E8:', cov(0x8003AF70, 0x8003B0E8))
    print('coverage sort  0x8003AE04..0x8003AF70:', cov(0x8003AE04, 0x8003AF70))
    return mism


if __name__ == '__main__':
    N = int(sys.argv[1]) if len(sys.argv) > 1 else 1500
    seed = int(sys.argv[2]) if len(sys.argv) > 2 else 20261018
    sys.exit(1 if main(N, seed) else 0)
