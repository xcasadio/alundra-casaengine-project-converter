"""arena_probe.py - critic of E20.f3.  The geometry of AlundraHeroChargeArenaTests (hero at x, a Beannoid 40 px east, same y) with the steel wave, in the oracle: control, and E10 removed (HitArena has the native handlers off, so the DLL's E10 would not run).
usage: python -B arena_probe.py   (writes ../out/arena_probe.txt).  Release tick R=96 in the oracle (the arena releases at 91): subtract 5 to read arena ticks."""
import sys, os
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
from wave_run import run_wave
from wave_mut import patch
OUT = os.path.join(HERE, '..', 'out')

def hits(res):
    out = []
    for r in res['rows']:
        t = r['slots'].get(1)
        if t and t['c1d0'] == 25:
            out.append((r['tick'], 'wave' if t['touch_slot'] == 2 else ('hero' if t['touch_slot'] == 0 else t['touch_slot'])))
    return out

def life(res):
    w = [(r['tick'], r['slots'][2]['status'], r['slots'][2]['box1d4'], r['slots'][2]['cnt1c']) for r in res['rows'] if r['slots'].get(2)]
    return w

lines = []
# target record (11, 11): px (144, 96); hero at (104, 96) -> rel (+40, 0), the arena's geometry
for name, init in (('control', None), ('E10_removed (natives off)', [patch((0x8007F244, 0x00000000))])):
    res = run_wave('a', direction='east', n=190, targets=[(11, 11, 0)], hero_px=(104, 96), init=init)
    h = hits(res)
    w = life(res)
    lines.append('%s: target hits (tick, attacker) %s' % (name, h))
    lines.append('   wave rows: first t%d last t%d, last status %d ; box flag at the last 4 rows: %s' % (w[0][0], w[-1][0], w[-1][1], [(t, b, c) for t, s, b, c in w[-4:]]))
open(os.path.join(OUT, 'arena_probe.txt'), 'w').write('\n'.join(lines) + '\n')
print('\n'.join(lines))
