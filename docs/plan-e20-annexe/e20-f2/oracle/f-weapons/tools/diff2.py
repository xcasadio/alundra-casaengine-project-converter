"""diff2.py - D1: the oracle's pipeline on two independent interpreters, tick by tick.

For every input script (S-A, S-B and N seeded random pad scripts of T ticks) two identical worlds are built (oracle.setup), one is driven by the oracle's
interpreter (emu.py, via world.tick), the other by mips.py (via interp2.tick2).  After every tick the hero's whole 0x294-byte slot, the stub journal, the
globals the passes write (0x80126FF4, 0x80126FFC, 0x80127008.., 0x80127108-0x80127114, 0x80127000) and the stats block are compared.
A single differing byte, or one interpreter raising where the other does not, is reported.
writes out/diff2.txt"""
import os
import random
import sys
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import oracle
import interp2
import world as W

GLOBAL_RANGES = [(0x80126FF0, 0x80127000), (0x80127000, 0x80127004), (0x80127008, 0x80127118), (0x801EB82C, 0x801EB82C + 0x240)]


def state_bytes(m):
    P = W.ent(0)
    parts = [bytes(m.ram[P & 0x1FFFFFFF:(P & 0x1FFFFFFF) + W.STRIDE])]
    for a, b in GLOBAL_RANGES:
        parts.append(bytes(m.ram[a & 0x1FFFFFFF:b & 0x1FFFFFFF]))
    return b''.join(parts)


def run_script(holds, label, out, skip=()):
    m1, _ = oracle.setup()
    m2, _ = oracle.setup()
    p1, p2 = W.Pad(), W.Pad()
    for t, h in enumerate(holds, 1):
        e1 = e2 = None
        try:
            m1.tick_no = t
            W.tick(m1, p1, h, skip=skip)
        except Exception as e:            # noqa
            e1 = '%s: %s' % (type(e).__name__, str(e)[:80])
        try:
            m2.tick_no = t
            interp2.tick2(m2, p2, h, skip=skip)
        except Exception as e:            # noqa
            e2 = '%s: %s' % (type(e).__name__, str(e)[:80])
        if e1 or e2:
            if e1 and e2 and e1.split(':')[0] == e2.split(':')[0]:
                out.append('%s: tick %d both interpreters stop (%s | %s)' % (label, t, e1, e2))
                return 'both-stop', t
            out.append('%s: tick %d DIFFERENT FAILURE emu.py=%s mips.py=%s' % (label, t, e1, e2))
            return 'fail', t
        s1, s2 = state_bytes(m1), state_bytes(m2)
        if s1 != s2:
            idx = [i for i in range(len(s1)) if s1[i] != s2[i]]
            out.append('%s: tick %d STATE DIFFERS at %d bytes, first offset %d (hold 0x%04X)' % (label, t, len(idx), idx[0], h))
            return 'diff', t
        if m1.journal != m2.journal:
            out.append('%s: tick %d JOURNAL DIFFERS' % (label, t))
            return 'diff', t
    return 'equal', len(holds)


def main(outdir, n=60, ticks=80, seed=20261009):
    out = []
    counts = {}
    scripts = [('S-A', [W.CROSS] + [0] * 23), ('S-B', [W.RIGHT] * 3 + [W.RIGHT | W.CROSS] + [W.RIGHT] * 23)]
    rnd = random.Random(seed)
    bits = [W.UP, W.RIGHT, W.DOWN, W.LEFT, W.TRIANGLE, W.CIRCLE, W.CROSS, W.SQUARE]
    for k in range(n):
        cur = 0
        seq = []
        for _ in range(ticks):
            r = rnd.random()
            if r < 0.25:
                cur ^= rnd.choice(bits)
            elif r < 0.27:
                cur = 0
            seq.append(cur)
        scripts.append(('random %02d' % k, seq))
    for label, seq in scripts:
        res, t = run_script(seq, label, out)
        counts[res] = counts.get(res, 0) + 1
        if res == 'equal':
            out.append('%s: %d ticks identical on both interpreters' % (label, t))
    out.append('SUMMARY: %s' % counts)
    open(os.path.join(outdir, 'diff2.txt'), 'w', encoding='utf-8').write('\n'.join(out) + '\n')
    print('\n'.join(out[-6:]))
    print('SUMMARY', counts)


if __name__ == '__main__':
    main(sys.argv[1] if len(sys.argv) > 1 else os.path.join(HERE, '..', 'out'))
