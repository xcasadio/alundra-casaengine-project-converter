"""diff2_hits.py - D2: the random sets on TWO independent interpreters (emu.py via machine.py, and mips.py via interp2.py).

Two worlds are built; the same seeded generator drives both; after every set the entity slots, the lists, the latched animation, the RNG word, the effect
journal and the TouchingEntity/HP of every slot must be byte-identical.  Writes outdir/diff2_hits.txt.  usage: python -B diff2_hits.py [outdir] [nsets]"""
import os
import random
import sys
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import hitworld as H
import cases as C
import random_sets as R
import interp2

RANGES = [(H.ENT0, H.ENT0 + 64 * H.STRIDE), (H.LIST, H.LIST + 0x210), (H.LAST, H.LAST + 4), (H.G_ANIM, H.G_ANIM + 4), (H.RNG, H.RNG + 4)]


def second_world():
    W2 = H.World()
    m = W2.m
    m.call = lambda addr, *a, limit=None, m=m: interp2.call2(m, addr, *a)
    return W2


def state(W):
    m = W.m
    b = b''.join(bytes(m.ram[a & 0x1FFFFFFF:b & 0x1FFFFFFF]) for a, b in RANGES)
    return b, list(m.journal)


def main(outdir, n=300, seed=R.SEED):
    os.makedirs(outdir, exist_ok=True)
    W1, W2 = H.World(), second_world()
    r1, r2 = random.Random(seed), random.Random(seed)
    stats = {}
    lines = []
    bad = 0
    for s in range(n):
        res1 = C.run_case(W1, R.gen_build(r1, stats))
        res2 = C.run_case(W2, R.gen_build(r2, stats))
        (b1, j1), (b2, j2) = state(W1), state(W2)
        if b1 != b2 or j1 != j2:
            bad += 1
            idx = [i for i in range(len(b1)) if b1[i] != b2[i]]
            lines.append('set %d: DIFFERS (%d bytes, first %s; journal equal %s)' % (s, len(idx), idx[:3], j1 == j2))
    lines.append('SUMMARY: %d sets, %d differing between emu.py and mips.py' % (n, bad))
    open(os.path.join(outdir, 'diff2_hits.txt'), 'w', encoding='utf-8').write('\n'.join(lines) + '\n')
    print(lines[-1])
    return 0 if bad == 0 else 1


if __name__ == '__main__':
    sys.exit(main(sys.argv[1] if len(sys.argv) > 1 else os.path.join(HERE, '..', 'out'), int(sys.argv[2]) if len(sys.argv) > 2 else 300))
