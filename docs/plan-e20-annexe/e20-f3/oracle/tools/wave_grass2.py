"""wave_grass2.py - bin agent: same as wave_grass.py but with a SPY on CheckAndTriggerTileEffect 0x8003A7B0 that records the caller (ra), the entity (a0 -> slot), its HitBox at the call
and the cells it cut, then runs the REAL function (hook removed during the side call).  Real code otherwise."""
import sys, os, re
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import pworld as P
import world as W
import c1world as C
from wave_run import run_wave, DIRS, RELEASE
from wave_grass import grass
OUT = os.path.join(HERE, '..', 'out')
ADDR = 0x8003A7B0
CALLS = []

def install_spy(m):
    def spy(mm):
        a0 = mm.r[4]
        ra = mm.r[31]
        slot = (a0 - P.ENT0) // P.STRIDE
        hb = (mm.peeks(a0 + 0x1FC), mm.peeks(a0 + 0x200), mm.peeks(a0 + 0x204), mm.peeks(a0 + 0x214), mm.peeks(a0 + 0x218), mm.peeks(a0 + 0x21C), mm.peek(a0 + 0x1D4))
        j0 = len(mm.journal)
        h = mm.hooks.pop(ADDR)
        sp = ((mm.r[29] - 0x800) & ~7)
        v0 = C.side_call(mm, ADDR, [a0], sp)
        mm.hooks[ADDR] = h
        cuts = [j for j in mm.journal[j0:] if j[1] == 'effect_3BDD8']
        CALLS.append((mm.tick_no, slot, ra, hb, len(cuts), [(( j[2][3] / 65536), (j[2][4] / 65536)) for j in [] ]))
        mm.r[2] = v0
    m.hooks[ADDR] = spy

if __name__ == '__main__':
    lines = []
    for direction in ('east', 'north'):
        del CALLS[:]
        res = run_wave('g', direction=direction, n=150, cells=grass, init=[install_spy])
        lines.append('# %s: CheckAndTriggerTileEffect calls (tick, slot, caller ra, HitBox x,y,z px, size w,d,h, box ptr, cells cut)' % direction)
        per = {}
        for t, slot, ra, hb, n, _ in CALLS:
            per.setdefault((slot, ra), []).append((t, hb, n))
        for (slot, ra), l in sorted(per.items()):
            cuts = [(t, n) for t, hb, n in l if n]
            lines.append('  slot %d caller ra=%08X: %d calls, first t%d last t%d; ticks with cuts: %s' % (slot, ra, len(l), l[0][0], l[-1][0], cuts))
        lines.append('  wave (slot 1) calls from ticks: %s' % [t for t, slot, ra, hb, n, _ in CALLS if slot == 1][:60])
        wcalls = [(t, hb) for t, slot, ra, hb, n, _ in CALLS if slot == 1]
        if wcalls:
            t, hb = wcalls[0]
            lines.append('  first wave call t%d hitbox x=%.3f y=%.3f z=%.5f size=(%.5f,%.5f,%.5f) ptr=%X' % (t, hb[0] / 65536, hb[1] / 65536, hb[2] / 65536, hb[3] / 65536, hb[4] / 65536, hb[5] / 65536, hb[6]))
        lines.append('  wave calls with cuts: %s' % [(t, n, ) for t, slot, ra, hb, n, _ in CALLS if slot == 1 and n])
    open(os.path.join(OUT, 'wave_grass2.txt'), 'w').write('\n'.join(lines) + '\n')
    print('\n'.join(lines))
