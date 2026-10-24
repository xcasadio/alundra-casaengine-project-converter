"""sc_throw.py - throw the BOMB (and the BEANS) east / west / north / south, standing still or walking, on flat ground: flight, landing, bounce, fuse.
usage: python -B sc_throw.py [item]"""
import sys
import h3_game as G
from h3_game import *

ITEM = int(sys.argv[1], 0) if len(sys.argv) > 1 else 0x23
SQ_AT = 50


def one(facing, walk, item=ITEM, n=140, sq_at=SQ_AT, jump_at=None):
    m, STATS = G.make(item, hero_px=(200, 200), hero_dir=G.DIRS[facing])
    d = G.HOLD[facing]

    def holds(t):
        h = 0
        if t <= 3:
            h |= d                                    # turn the hero
        if t == 4:
            h |= G.CIRCLE
        if t == sq_at:
            h |= G.SQUARE | (d if walk else 0)
        elif walk and t > sq_at:
            h |= d
        elif walk and t > 4 and t < sq_at:
            h |= 0
        if jump_at is not None and t == jump_at:
            h |= G.CROSS
        return h
    r = Run(m, holds)
    ev = []
    prev = None
    bomb = None
    hx0 = hy0 = None
    for t in range(1, n + 1):
        r.step(1)
        if bomb is None:
            for i in range(1, 8):
                if m.peeks(W.ent(i) + 0x10) and m.peeks(W.ent(i) + 0x68) in (1, 2):
                    bomb = W.ent(i)
        if bomb is None:
            continue
        s = e_snap(m, bomb)
        h = e_snap(m, HERO)
        if prev is None or any(prev[k] != s[k] for k in ('st', 'ta', 'c28', 'gr', 'adj', 't24', 'dr', 'hitc')):
            ev.append('t%03d bomb st=%d ta=%d dr=%d c28=%s t24=%d gr=%d adj=%d hitc=%d pos=(%.2f,%.2f,%.2f) | hero ta=%#x pos=(%.2f,%.2f,%.2f) c2c=%s' % (
                t, s['st'], s['ta'], s['dr'], '%#x' % s['c28'], s['t24'], s['gr'], s['adj'], s['hitc'], px(s['x']), px(s['y']), px(s['z']), h['ta'], px(h['x']), px(h['y']), px(h['z']), '%#x' % m.peek(HERO + 0x2C)))
        prev = s
    # summary
    return ev, r


if __name__ == '__main__':
    for facing in ('right', 'left', 'up', 'down'):
        for walk in (False, True):
            ev, r = one(facing, walk)
            print('=== item %#x facing %s %s' % (ITEM, facing, 'walking' if walk else 'standing'))
            for e in ev[:12]:
                print('  ' + e)
