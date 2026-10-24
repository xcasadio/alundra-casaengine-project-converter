"""pass38634.py - D1: what the first pass of the tick, 0x80038634, does, shown by running its real code on prepared entity arrays.

Decoded (see notes.md for the instruction words): for i in 0..63, slot i = 0x80127D30 + 0x294*i:
    if slot.status (+0x10) == 4:  copy 0x294 bytes from the staging entity 0x80134368 into the slot, then slot.index (+0) = i   (the slot is not counted this pass)
    elif slot.status != 0:        last = i
  [0x80134360] = last          (the highest index of a non-empty slot, 0 when every slot is empty)
writes out/pass-80038634.txt"""
import os
import sys
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
from machine import Machine
import world as W

STAGING = 0x80134368


def fresh():
    m = Machine()
    m.zero(W.ENT0, 64 * W.STRIDE)
    m.zero(0x80134360, 0x400)
    return m


def run(m):
    m.call(0x80038634)
    return m.peeks(0x80134360)


def main(outdir):
    L = []
    m = fresh(); m.poke(0x80134360, 7)
    m.poke(W.ent(0) + 0x10, 2)
    L.append('hero (slot 0) status 2 alone, [0x80134360] preset 7          -> %d (expected 0: the hero is the last non-empty slot)' % run(m))
    m = fresh(); m.poke(0x80134360, 7)
    L.append('every slot empty, [0x80134360] preset 7                       -> %d' % run(m))
    m = fresh()
    for i, st in ((0, 2), (5, 1), (9, 3), (20, 0)):
        m.poke(W.ent(i) + 0x10, st)
    L.append('slots 0:2, 5:1, 9:3, 20:0                                     -> %d (expected 9)' % run(m))
    m = fresh()
    m.poke(W.ent(0) + 0x10, 2)
    m.poke(W.ent(3) + 0x10, 4)
    m.poke(STAGING + 0x10, 1); m.poke(STAGING + 0x100, 0xABCD1234); m.poke(STAGING + 0x290, 0x5A5A5A5A)
    r = run(m)
    L.append('slot 3 status 4 + staging entity (status 1, marker at +0x100)  -> [0x80134360] %d; slot 3: status %d, index +0 = %d, +0x100 = 0x%08X, +0x290 = 0x%08X' % (
        r, m.peeks(W.ent(3) + 0x10), m.peeks(W.ent(3)), m.peek(W.ent(3) + 0x100), m.peek(W.ent(3) + 0x290)))
    r2 = run(m)
    L.append('second run of the pass on the same array                      -> %d (the copied slot is now counted)' % r2)
    L.append('the hero slot is read by the pass (its +0x10), and the pass writes the one word [0x80134360] that 0x800384F4 and 0x800386D0 use as the entity count.')
    open(os.path.join(outdir, 'pass-80038634.txt'), 'w', encoding='utf-8').write('\n'.join(L) + '\n')
    print('\n'.join(L))


if __name__ == '__main__':
    main(sys.argv[1] if len(sys.argv) > 1 else os.path.join(HERE, '..', 'out'))
