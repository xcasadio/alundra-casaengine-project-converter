"""explore.py - D1: what the oracle reaches beyond S-A and S-B (for the slices that will use it): random pad scripts on the oracle's interpreter, the set of
animations the hero visits, the BSS words read before any write (these need an explicit starting value in a slice that goes there), the stubbed calls hit.
writes out/explore.txt"""
import os
import random
import sys
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import oracle
import world as W


def main(outdir, n=60, ticks=80, seed=20261009):
    rnd = random.Random(seed)
    bits = [W.UP, W.RIGHT, W.DOWN, W.LEFT, W.TRIANGLE, W.CIRCLE, W.CROSS, W.SQUARE]
    anims = {}
    uninit = {}
    stubs = {}
    calls = {}
    for k in range(n):
        m, _ = oracle.setup()
        pad = W.Pad()
        cur = 0
        for t in range(1, ticks + 1):
            r = rnd.random()
            if r < 0.25:
                cur ^= rnd.choice(bits)
            elif r < 0.27:
                cur = 0
            m.tick_no = t
            W.tick(m, pad, cur)
            a = m.peeks(W.ent(0) + 0x90)
            anims[a] = anims.get(a, 0) + 1
        for key, c in m.uninit_reads.items():
            uninit[key] = uninit.get(key, 0) + c
        for j in m.journal:
            stubs[j[1]] = stubs.get(j[1], 0) + 1
        for f, c in m.calls.items():
            calls[f] = calls.get(f, 0) + c
    L = ['# %d random scripts of %d ticks (seed %d), oracle interpreter, hero alone' % (n, ticks, seed),
         '# animations visited (current animation id: ticks): ' + ', '.join('0x%02X: %d' % kv for kv in sorted(anims.items())),
         '# stubbed calls hit (kind: count): ' + ', '.join('%s: %d' % kv for kv in sorted(stubs.items())),
         '# reads of bytes nobody wrote (pc, address, size: count):']
    for (pc, a, sz), c in sorted(uninit.items(), key=lambda kv: (kv[0][1], kv[0][0])):
        L.append('  pc %08x addr %08x size %d: %d' % (pc, a, sz, c))
    L.append('# functions entered (count): ' + ' '.join('%08x:%d' % kv for kv in sorted(calls.items())))
    open(os.path.join(outdir, 'explore.txt'), 'w', encoding='utf-8').write('\n'.join(L) + '\n')
    print('\n'.join(L[:4]))
    print(len(uninit), 'uninitialised-read sites')


if __name__ == '__main__':
    main(sys.argv[1] if len(sys.argv) > 1 else os.path.join(HERE, '..', 'out'))
