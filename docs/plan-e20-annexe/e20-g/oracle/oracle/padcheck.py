"""padcheck.py - D1: the binary's pad routine 0x8002E250 against the port AlundraTickPad.Update (as world.Pad), word by word.

The routine is run for real in the emulator on a pad struct initialised as 0x8002E34C / 0x8002E30C leave it; its four output halfwords
(+0x10 hold, +0x12 pressed, +0x14 released, +0x16 pressed-with-repeat) are compared with the port's after every tick, on
  * the hold sequences of S-A and S-B,
  * long holds (the repeat counter: 20 ticks of silence, then the held buttons every tick),
  * random sequences (seeded) over buttons {Up, Right, Down, Left, Triangle, Circle, Cross, Square}.
writes out/padcheck.txt"""
import os
import random
import sys
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
from machine import Machine
import world as W


def check(seq, label, m=None):
    m = m or Machine()
    W.init_pad2(m)
    pad = W.Pad()
    n = 0
    for hold in seq:
        pad.update(hold)
        m.call(0x8002E250, W.PAD2, hold)
        got = (m.peek(W.PAD2 + 0x10) & 0xFFFF, m.peek(W.PAD2 + 0x12) & 0xFFFF, m.peek(W.PAD2 + 0x14) & 0xFFFF, m.peek(W.PAD2 + 0x16) & 0xFFFF)
        want = (pad.hold, pad.pressed, pad.released, pad.byint)
        if got != want:
            return '%s: DIFFERENCE at tick %d (hold 0x%04X): binary %s port %s' % (label, n + 1, hold, got, want)
        n += 1
    return '%s: %d ticks equal (4 words each)' % (label, n)


def main(outdir):
    L = []
    L.append(check([W.CROSS] + [0] * 23, 'S-A hold sequence'))
    L.append(check([W.RIGHT] * 3 + [W.RIGHT | W.CROSS] + [W.RIGHT] * 23, 'S-B hold sequence'))
    L.append(check([W.RIGHT] * 70 + [0] * 3 + [W.RIGHT | W.CROSS] * 30 + [W.CROSS] * 25, 'long holds (repeat after 20 ticks)'))
    rnd = random.Random(20261009)
    bits = [W.UP, W.RIGHT, W.DOWN, W.LEFT, W.TRIANGLE, W.CIRCLE, W.CROSS, W.SQUARE]
    for k in range(40):
        seq = []
        cur = 0
        for _ in range(300):
            r = rnd.random()
            if r < 0.15:
                cur ^= rnd.choice(bits)              # toggle one button
            elif r < 0.17:
                cur = 0
            seq.append(cur)
        L.append(check(seq, 'random sequence %02d (300 ticks)' % k))
    ok = all('DIFFERENCE' not in l for l in L)
    L.append('VERDICT: ' + ('the port equals the binary routine on every tick of every sequence' if ok else 'DIFFERENCE FOUND'))
    open(os.path.join(outdir, 'padcheck.txt'), 'w', encoding='utf-8').write('\n'.join(L) + '\n')
    print('\n'.join(L[:5]), '...', L[-1])


if __name__ == '__main__':
    main(sys.argv[1] if len(sys.argv) > 1 else os.path.join(HERE, '..', 'out'))
