"""sc_explosion.py - what the bomb explosion hits.  The hero throws the bomb east from (200,200); it lands at (241.34,200,0) and waits.  From a RAM snapshot taken after the landing, ONE stationary target (global
sprite 25, ClassA, balance sheet response 16 to every attack type, HP 1000) is placed at an offset from the bomb, the run goes on to the end of the explosion, and the damage events are read as the
D handler of an enemy does (REAL ComputeNewHp 0x8003A374 on the target left with a TouchingEntity by the REAL hit pass 0x80039300, at the head of the next tick).
usage: python -B sc_explosion.py [dx,dy,dz ...]   default grid"""
import sys
import h3_game as G
from h3_game import *

HIT_FROM = 100


def prepare():
    m, STATS = G.make(0x23, hero_px=(200, 200), hero_dir=G.DIRS['right'])
    def holds(t):
        h = 0
        if t <= 3: h |= G.RIGHT
        if t == 4: h |= G.CIRCLE
        if t == 50: h |= G.SQUARE
        return h
    r = Run(m, holds)
    r.step(HIT_FROM)
    return m, r


def find_bomb(m):
    for i in range(1, 8):
        if m.peeks(W.ent(i) + 0x10) and m.peeks(W.ent(i) + 0x68) == 2:
            return W.ent(i)


def probe(m, r, snap, offs, hp=1000, sheet=None, z_extra=0):
    m.ram[:] = snap[0]; m.init[:] = snap[1]
    b = find_bomb(m)
    bx, by, bz = m.peeks(b + 0x114) >> 16, m.peeks(b + 0x118) >> 16, m.peeks(b + 0x11C)
    dx, dy = offs
    tp = G.add_target(m, bx + dx, by + dy, hp=hp, resp_sheet=sheet)
    m.poke(tp + 0x11C, bz + z_extra)
    hits = []
    pad_hold = 0
    for t in range(HIT_FROM + 1, 340):
        tc = m.peek(tp + 0x224)
        if tc:
            before = m.peeks(tp + 0x14)
            m.call(0x8003A374, tp)
            hits.append((t, m.peeks(tc + 0x68), before - m.peeks(tp + 0x14)))
            m.poke(tp + 0x224, 0)
        m.tick_no = t
        W.tick(m, r.pad, 0, check_pad=False)
    return hits


if __name__ == '__main__':
    m, r = prepare()
    snap = (bytes(m.ram), bytes(m.init))
    if len(sys.argv) > 1:
        grid = [tuple(int(v) for v in a.split(',')) for a in sys.argv[1:]]
    else:
        grid = [(dx, dy) for dx in (0, 16, 24, 30, 36, 40, 44, 48, 52, 56) for dy in (0, 12, 20, 24, 28, 32, 36)]
    print('bomb landed at', [round(m.peeks(find_bomb(m) + o) / 65536, 2) for o in (0x114, 0x118, 0x11C)])
    rows = {}
    for off in grid:
        h = probe(m, r, snap, off)
        rows[off] = h
    for dy in sorted(set(o[1] for o in grid)):
        print('dy=%3d  ' % dy + ' '.join(('%3d:%s' % (dx, ('%d@%d' % (rows[(dx, dy)][0][2], rows[(dx, dy)][0][0])) if rows[(dx, dy)] else '-  ')) for dx in sorted(set(o[0] for o in grid) ) if (dx, dy) in rows))
