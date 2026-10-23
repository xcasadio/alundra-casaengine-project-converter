"""loot_check.py - E20.e rev3 T0 (k2): a cut costs 1 or 2 words of the generator and nothing else draws (the stop condition of the plan).
The REAL ChooseRandomlyAnItem (0x80032968) on the machine, 12 000 random generator states plus one state per high nibble of the first step, code 0xFF: the item and the number of steps against
cutlib.choose (the model the vectors and the seeds use).  Writes out/loot_check.txt.  Exit 0 = 0 mismatch and every roll cost 1 or 2 steps.
"""
import os
import random
import sys

import cutlib as L
import c1world as C

OUT = L.OUT


def steps(a, b):
    k = 0
    while a != b:
        a = L.rng_step(a)
        k += 1
        if k > 5:
            return None
    return k


def main():
    m, meta = L.P.setup(item=None, hero_px=(100, 100), seed=1)
    rng = random.Random(20261022)
    states = [rng.getrandbits(32) for _ in range(12000)]
    # one state per high nibble of the FIRST step (the draw that picks the link of row 0x7F), and per nibble of the second step for the chained codes
    for nib in range(16):
        while True:
            s = rng.getrandbits(32)
            if L.rng_step(s) >> 28 == nib:
                states.append(s)
                break
    bad = 0
    cost = {}
    items = {}
    for s in states:
        m.poke(L.RNG, s)
        v = C.side_call(m, 0x80032968, [0xFF], 0x801FF000)
        after = m.peek(L.RNG) & L.M32
        item, draws, s2 = L.choose(0xFF, s)
        k = steps(s, after)
        if v != item or after != s2 or k != draws:
            bad += 1
        cost[k] = cost.get(k, 0) + 1
        items[v] = items.get(v, 0) + 1
    ok = bad == 0 and set(cost) <= {1, 2}
    lines = ['rolls=%d mismatches=%d cost(steps)=%s items=%s' % (len(states), bad, dict(sorted(cost.items())), dict(sorted(items.items()))),
             'every roll costs 1 or 2 steps: %s' % (set(cost) <= {1, 2})]
    open(os.path.join(OUT, 'loot_check.txt'), 'w').write('\n'.join(lines) + '\n')
    print('\n'.join(lines))
    return 0 if ok else 1


if __name__ == '__main__':
    sys.exit(main())
