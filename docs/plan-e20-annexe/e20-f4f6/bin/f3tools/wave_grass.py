"""wave_grass.py - bin agent, E20.f3 discovery: what C61 = CheckAndTriggerTileEffect(wave) cuts, tick by tick.  Real code (pworld).
All cells are grass (walkability bit 0x02), height 0.  The effect/sound stubs journal the cut: fx(id=<map header byte 9>) at the cell centre, sound 0x1F, plus the real loot roll 0x80032C7C (RNG).
Cells cut before tick 117 belong to the hero's own swing animations (also CheckAndTriggerTileEffect callers), so the analysis starts at the wave's spawn tick S = 117."""
import sys, os, re
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import pworld as P
import world as W
import c1world as C
from wave_run import run_wave, DIRS, RELEASE
OUT = os.path.join(HERE, '..', 'out')

def grass(c, r):
    return (0, 0, 2, 0)

def cut_events(res):
    ev = []
    for r in res['rows']:
        for j in r['journal']:
            m = re.match(r'fx\(id=(\d+),a2=\d+,x=([-\d.]+),y=([-\d.]+),z=([-\d.]+)\)', j)
            if m and int(m.group(1)) == 2:
                x, y, z = float(m.group(2)), float(m.group(3)), float(m.group(4))
                ev.append((r['tick'], int((x - 12) / 24), int((y - 8) / 16), x, y, z, r['rng'] & 0xFFFFFFFF))
    return ev

if __name__ == '__main__':
    lines = []
    for direction in ('east', 'north'):
        res = run_wave('g', direction=direction, n=150, cells=grass)
        ev = cut_events(res)
        wave_ev = [e for e in ev if e[0] >= 117]
        lines.append('# %s: all %d cut events; at or after the wave spawn (t >= 117): %d' % (direction, len(ev), len(wave_ev)))
        for e in ev:
            lines.append('   t%03d cell(col=%d,row=%d) effect pos=(%.1f,%.1f) z=%.1f rng_after=%08X%s' % (e[0], e[1], e[2], e[3], e[4], e[5], e[6], '   <-- wave' if e[0] >= 117 else ''))
        # sound ids 0x1F count
        snd = sum(1 for r in res['rows'] for j in r['journal'] if j == 'snd(31)')
        lines.append('   sound 0x1F (31) journalled %d times' % snd)
    open(os.path.join(OUT, 'wave_grass.txt'), 'w').write('\n'.join(lines) + '\n')
    print('\n'.join(lines))
