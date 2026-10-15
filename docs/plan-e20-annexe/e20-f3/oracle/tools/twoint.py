"""twoint.py - bin agent: the wave scenarios on the two independent interpreters of the oracle (emu.py and mips.py through interp2.tick2); rows, journal and RNG must be identical."""
import sys, os
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
from wave_run import run_wave
OUT = os.path.join(HERE, '..', 'out')
lines = []
ok = True
for sword, d in (('steel', 'east'), ('steel', 'north'), ('demon', 'west'), ('holy', 'south')):
    a = run_wave('a', sword=sword, direction=d, n=150)
    b = run_wave('b', sword=sword, direction=d, n=150, interp=True)
    same = True
    for ra, rb in zip(a['rows'], b['rows']):
        if ra['slots'] != rb['slots'] or ra['journal'] != rb['journal'] or ra['rng'] != rb['rng']:
            lines.append('%s %s DIFF at tick %d' % (sword, d, ra['tick']))
            same = False
            break
    lines.append('%s %s: two interpreters identical over %d ticks (slots, journal, RNG): %s' % (sword, d, len(a['rows']), same))
    ok = ok and same
open(os.path.join(OUT, 'two_interpreters.txt'), 'w').write('\n'.join(lines) + '\n')
print('\n'.join(lines))
sys.exit(0 if ok else 1)
