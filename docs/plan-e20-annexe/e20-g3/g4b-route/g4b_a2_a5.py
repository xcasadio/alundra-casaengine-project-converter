"""g4b_a2_a5.py - E20.g4b T0 (a2) to (a5): the starts of the arcs (b2) to (b5) on the binary, with the 12 records (my392 harness).  A scripted player reads the machine before each tick; nothing is written after the start.
 a2: hero at (670, 316, 64) facing west, Left until the contact xcoll = rec6 (y in [311 ; 327[), Square at the next tick (lift); the fall of rec10 per tick.
 a3: hero at (636, 296) standing on rec5 (z 80), Down until the contact xcoll = rec10 (y limited to 297.0), Square at the next tick.
 a4: hero at (720, 298, 64) facing west, Left held, Cross at the tick tj = 12 (counted from the pose), Square at the first tick whose previous end had xcoll = rec10 and the hero in the air.
 a5: hero at (657, 312, 64) flush on rec6, facing north, Up until the contact xcoll = rec5 (y = 295.0), Square at the next tick.
Usage: python -B -I g4b_a2_a5.py  -> out/g4b_a2.txt .. out/g4b_a5.txt"""
import os
import sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import g4b_lib as L
G = L.G
U = L.U


def run(s, pilot, ticks, extra_recs=()):
    out = []
    for _ in range(ticks):
        h = s.hero()
        hold = pilot(s, h)
        s.tick(hold)
        r = s.rows[-1]
        extra = ' '.join('rec%d(x=%.3f y=%.3f z=%.4f st=%d an=%d sup=%s)' % ((e,) + tuple(s.crate(e)[k] for k in ('x', 'y', 'z', 'status', 'anim', 'support'))) for e in extra_recs)
        out.append(L.fmt_row(r, extra))
    return out


def a2():
    s = L.Sim((670, 316), 8, x_raw=670 * U, y_raw=316 * U)
    st = {'sq': None}

    def pilot(s, h):
        if st['sq'] is None:
            hold = G.LEFT
            if h['xcoll'] == 6:
                st['sq'] = s.t + 1
                return G.LEFT | G.SQUARE
            return hold
        return G.LEFT
    rows = run(s, pilot, 60, extra_recs=(6, 10))
    sq = st['sq']
    series = []
    for i, r in enumerate(rows):
        pass
    # rec10 z series: read after each tick
    return s, rows, sq


def a3():
    s = L.Sim((636, 296), 8, x_raw=636 * U, y_raw=296 * U, z=None)
    s.m.poke(L.HERO + 0x11C, (80 * U + 1) & 0xFFFFFFFF)
    st = {'sq': None}

    def pilot(s, h):
        if st['sq'] is None:
            if h['xcoll'] == 10:
                st['sq'] = s.t + 1
                return G.DOWN | G.SQUARE
            return G.DOWN
        return G.DOWN
    rows = run(s, pilot, 60, extra_recs=(10, 6))
    return s, rows, st['sq']


def a4(tj=12):
    s = L.Sim((720, 298), 8, x_raw=720 * U, y_raw=298 * U)
    st = {'sq': None}

    def pilot(s, h):
        t = s.t + 1
        hold = G.LEFT | (G.CROSS if t == tj else 0)
        if st['sq'] is None and t > tj and h['xcoll'] == 10 and h['ground'] == 0:
            st['sq'] = t
            hold |= G.SQUARE
        return hold
    rows = run(s, pilot, 70, extra_recs=(10, 5))
    return s, rows, st['sq']


def a5():
    s = L.Sim((657, 312), 16, x_raw=657 * U, y_raw=312 * U)
    st = {'sq': None}

    def pilot(s, h):
        if st['sq'] is None:
            if h['xcoll'] == 5:
                st['sq'] = s.t + 1
                return G.UP | G.SQUARE
            return G.UP
        return G.UP
    rows = run(s, pilot, 60, extra_recs=(5,))
    return s, rows, st['sq']


def main():
    # a2
    s, rows, sq = a2()
    out = ['E20.g4b T0 (a2) - start (670, 316, z 64) facing west, Left until the contact with rec6, Square the tick after; 12 records (binary)',
           'Square pressed at tick %s' % sq]
    h = s.rows
    # the fall of rec10: need rec10 z per tick: re-extract from the printed rows
    zs = []
    for line in rows:
        pass
    out += rows
    open(os.path.join(G.OUT, 'g4b_a2.txt'), 'w', encoding='utf-8').write('\n'.join(out) + '\n')
    # a3
    s, rows, sq = a3()
    out = ['E20.g4b T0 (a3) - start (636, 296, z 80+1 unit) standing on rec5, Down until the contact with rec10, Square the tick after; 12 records (binary)', 'Square pressed at tick %s' % sq] + rows
    open(os.path.join(G.OUT, 'g4b_a3.txt'), 'w', encoding='utf-8').write('\n'.join(out) + '\n')
    s, rows, sq = a4()
    out = ['E20.g4b T0 (a4) - start (720, 298, z 64) facing west, Left held, Cross at tick tj = 12, Square at the first tick after a tick that ended with the contact rec10 and the hero in the air; 12 records (binary)', 'Square pressed at tick %s' % sq] + rows
    open(os.path.join(G.OUT, 'g4b_a4.txt'), 'w', encoding='utf-8').write('\n'.join(out) + '\n')
    s, rows, sq = a5()
    out = ['E20.g4b T0 (a5) - start (657, 312, z 64) flush on rec6 facing north, Up until the contact with rec5, Square the tick after; 12 records (binary)', 'Square pressed at tick %s' % sq] + rows
    open(os.path.join(G.OUT, 'g4b_a5.txt'), 'w', encoding='utf-8').write('\n'.join(out) + '\n')


if __name__ == '__main__':
    main()
