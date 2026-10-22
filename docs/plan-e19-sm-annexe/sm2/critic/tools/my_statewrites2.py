"""Recursive version: follow jal callees that receive the state pointer, collect all offsets stored relative to S."""
import sys
sys.path.insert(0, 'tools')
from my_statewrites import *

cache = {}
def closure(entry, regs0, depth=0, path=()):
    key = (entry, tuple(sorted(regs0.items())))
    if key in cache: return cache[key]
    cache[key] = (set(), [])   # recursion guard
    st, calls = scan(entry, dict(regs0))
    allst = set(st); unk = []
    for c, args in calls:
        if c == 'jalr':
            unk.append(('jalr', args)); continue
        if depth > 6:
            unk.append(('depth', hex(c))); continue
        s2, u2 = closure(c, args, depth + 1)
        allst |= s2; unk += u2
    cache[key] = (allst, unk)
    return cache[key]

if __name__ == '__main__':
    P = {0xC: 'P2', 0x10: 'P3', 0x14: 'P4'}
    writers = {0xC: [], 0x10: [], 0x14: []}
    for opc in range(0, 0x100):
        h = u32(0x80098FAC + 4 * opc)
        if not (taddr <= h < taddr + tsize): continue
        st, unk = closure(h, {7: ('S', 0)})
        offs = sorted(set(o for o, sz in st))
        hit = [o for o in offs if o in (0xC, 0x10, 0x14)]
        for o in hit: writers[o].append(opc)
        extra = [hex(o) for o in offs if o not in (0x2C,)]
        if unk: print(f"{opc:02X} UNRESOLVED {unk}")
        if hit or [o for o in offs if o not in (0x2C, 0x8, 0x30, 0x34)]:
            print(f"{opc:02X} stores rel S: {[hex(o) for o in offs]}")
    for o, lst in writers.items():
        print(P[o], 'writers in binary (all slots):', ' '.join(f"{x:02X}" for x in lst))
