"""sc_hurt.py - the hero is hurt by a bomb explosion while carrying a second bomb: what happens to the carried bomb, the item state, the hero.
Bomb 1 is thrown east at t50 and lands at about (241,200); bomb 2 is started at t90 (Circle) and carried; the hero walks east to stand next to bomb 1 before it explodes (about t246).
usage: python -B sc_hurt.py [walk_until_x]"""
import sys
import h3_game as G
from h3_game import *

WALK_X = int(sys.argv[1]) if len(sys.argv) > 1 else 222


def run():
    m, STATS = G.make(0x23, hero_px=(200, 200), hero_dir=G.DIRS['right'])
    state = {'walk': False}

    def holds(t):
        h = 0
        if t <= 3: h |= G.RIGHT
        if t == 4: h |= G.CIRCLE
        if t == 50: h |= G.SQUARE
        if t == 90: h |= G.CIRCLE
        if 140 <= t and m.peeks(HERO + 0x114) >> 16 < WALK_X: h |= G.RIGHT
        return h
    r = Run(m, holds)
    last = {}
    out = []
    for t in range(1, 420):
        r.step(1)
        hero = (m.peeks(HERO + 0x14), m.peeks(HERO + 0x88), m.peek(HERO + 0x2C), m.peeks(0x80127164), m.peeks(HERO + 0x1D0), m.peeks(HERO + 0x1CC))
        b2 = None
        for i in range(1, 10):
            e = W.ent(i)
            if m.peeks(e + 0x10) and m.peeks(e + 0x68) == 2:
                b2 = e
        key = (hero, tuple((W.ent(i), m.peeks(W.ent(i) + 0x10), m.peeks(W.ent(i) + 0x88), m.peek(W.ent(i) + 0x28), m.peeks(W.ent(i) + 0x24)) for i in range(1, 10) if m.peeks(W.ent(i) + 0x68) == 2))
        if key != last.get('k'):
            hx = m.peeks(HERO + 0x114) / 65536.0
            line = 't%03d hero hp=%d ta=%#x c2c=%#x inuse=%#x inv1D0=%d blink1CC=%d x=%.1f' % (t, hero[0], hero[1], hero[2], hero[3], hero[4], hero[5], hx)
            for i in range(1, 10):
                e = W.ent(i)
                if m.peeks(e + 0x68) == 2 and m.peeks(e + 0x10):
                    line += ' | B%d st=%d ta=%d c28=%#x t24=%d pos=(%.1f,%.1f,%.1f) fz=%d' % (i, m.peeks(e + 0x10), m.peeks(e + 0x88), m.peek(e + 0x28), m.peeks(e + 0x24), m.peeks(e + 0x114) / 65536.0, m.peeks(e + 0x118) / 65536.0, m.peeks(e + 0x11C) / 65536.0, m.peeks(e + 0xB8) >> 16)
            out.append(line)
            last['k'] = key
    return out, r


if __name__ == '__main__':
    out, r = run()
    for l in out:
        print(l)
    print('journal tail:', [j for j in r.js if j[1] in ('sound',) and j[0] > 240][:20])
