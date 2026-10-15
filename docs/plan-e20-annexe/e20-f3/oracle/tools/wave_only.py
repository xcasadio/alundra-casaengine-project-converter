"""wave_only.py - bin agent, E20.f3 discovery: the wave as the ONLY attacker (the hero's HitsClassA flag bit 2 is cleared by the harness so that his own charged-swing boxes never contest the target's
+0x1D0 window).  Real code (pworld).  Reach sweep in four directions with per-target hit ticks; and the double hit (S and S+25)."""
import sys, os
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import pworld as P
import world as W
from wave_run import run_wave
OUT = os.path.join(HERE, '..', 'out')

def no_hero_hits(m):
    m.poke(P.ENT0 + 0x6C, m.peek(P.ENT0 + 0x6C) & ~4)

def hits(direction, xp, yp, sword='steel'):
    res = run_wave('w', sword=sword, direction=direction, n=150, targets=[(xp, yp, 0)], init=[no_hero_hits])
    h = [(r['tick'], r['slots'][1]['touch_slot']) for r in res['rows'] if r['slots'].get(1) and r['slots'][1]['c1d0'] == 25]
    return h

if __name__ == '__main__':
    lines = ['# the wave alone (hero HitsClassA cleared), steel wave S=117, hero (100,100); target = real Beannoid of map 14 (body offset (-10,-7,0), 20x14x8 +1 ulp), map record (xpos,ypos) -> px (12*xpos+12, 8*ypos+8)',
             '# each entry: (hit tick, TouchingEntity slot); slot 2 = the wave']
    plan = (('east', [(xp, 11) for xp in range(6, 19)]), ('north', [(7, yp) for yp in range(2, 12)]), ('west', [(xp, 11) for xp in range(1, 9)]), ('south', [(7, yp) for yp in range(10, 21)]))
    for direction, specs in plan:
        for xp, yp in specs:
            h = hits(direction, xp, yp)
            lines.append('%-5s target px=(%3d,%3d) rel=(%+4d,%+4d) hits=%s' % (direction, 12 * xp + 12, 8 * yp + 8, 12 * xp + 12 - 100, 8 * yp + 8 - 100, h))
            sys.stdout.flush()
    open(os.path.join(OUT, 'wave_only_sweep.txt'), 'w').write('\n'.join(lines) + '\n')
    print('\n'.join(lines))
