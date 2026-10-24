"""sc_beans.py - the BEANS (item 0x1F, global sprite 1) in the emulated game: start, carry, throw, landing, E21; thrown onto a fake 'Pot magique' (type 0x199); hurt while carrying; deep water; fuse-less carry.
usage: python -B sc_beans.py NAME     NAME in: basic pot hurt water carry_long
Harness writes: pad words, the cell field, a fake pot (global sprite 25 re-typed 0x199 and given the Gravity|Collidable bits), a second bomb (hurt scenario)."""
import sys
import h3_game as G
import sc_misc as SCM
from h3_game import *


def sc_basic():
    m, STATS = G.make(0x1F, hero_px=(200, 200))
    r = Run(m, SCM.basic_holds(square_at=50))
    for l in SCM.watch(m, r, 140, slots_types=(1,), keys=('st', 'ta', 'c28', 'gr', 'aend', 't24', 'fl')): print(l)
    print('journal (non-sound):', [(j[0], j[1], j[2][:7] if isinstance(j[2], (list, tuple)) else j[2:]) for j in r.js if j[1] != 'sound'][:8])


def sc_pot():
    m, STATS = G.make(0x1F, hero_px=(200, 200))
    pot = G.spawn_global(m, 25, 241, 200)
    m.poke(pot + 0x68, 0x199)                               # harness write: the 'Pot magique' type (0x100 + map sprite 153)
    m.poke(pot + 0x6C, m.peek(pot + 0x6C) | 0x180)          # Collidable + Gravity so that the rider pass (0x800364C8) can lend it as a support
    print('pot flags %#x size %s' % (m.peek(pot + 0x6C), [m.peeks(pot + o) >> 16 for o in (0x1DC, 0x1E0)]))
    r = Run(m, SCM.basic_holds(square_at=50))
    def extra(m, t):
        s = m.peek(pot + 0x12C)
        return None
    for l in SCM.watch(m, r, 120, slots_types=(1, 0x199), keys=('st', 'ta', 'c28', 'gr', 'aend', 'sup')): print(l)
    print('pot anim', m.peeks(pot + 0x88), 'pot status', m.peeks(pot + 0x10))


def sc_hurt():
    m, STATS = G.make(0x1F, hero_px=(200, 200))
    r = Run(m, SCM.basic_holds())
    r.step(60)
    p = G.spawn_global(m, 2, 215, 200)                      # harness write: a bomb about to explode next to the hero (fuse 3)
    m.poke(p + 0x274, 3)
    for l in SCM.watch(m, r, 90, slots_types=(1, 2), keys=('st', 'ta', 'c28', 't24', 'gr')): print(l)


def sc_water():
    def cells(col, row):
        return (0, 0, 0, 0x08) if col >= 10 else (0, 0, 0, 0)
    m, STATS = G.make(0x1F, hero_px=(200, 200), cells=cells, extra_items=(0x1B,))
    r = Run(m, SCM.basic_holds(extra=lambda t: G.RIGHT if t > 60 else 0))
    for l in SCM.watch(m, r, 100, slots_types=(1,), keys=('st', 'ta', 'c28', 't24')): print(l)
    print('journal (non-sound):', [(j[0], j[1], j[2][:7] if isinstance(j[2], (list, tuple)) else j[2:]) for j in r.js if j[1] != 'sound'][:6])


def sc_carry_long():
    m, STATS = G.make(0x1F, hero_px=(200, 200))
    r = Run(m, SCM.basic_holds())
    for l in SCM.watch(m, r, 700, slots_types=(1,), keys=('st', 'ta', 'c28', 't24')): print(l)


SC = {n[3:]: f for n, f in list(globals().items()) if n.startswith('sc_')}
if __name__ == '__main__':
    SC[sys.argv[1]]()
