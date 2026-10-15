"""k0_magnet.py - E20.e0k2 T0: the 3 px magnet of an entity with gravity, ForceZ == 0 and an XY force, arriving on a terrain that is 3 px higher (the magnet poses it, the full step is accepted)
and 4 px higher (no magnet: the first attempt is refused and the step is refined).  Same entity as k0_walker.py: the Beannoid of map 14 (sprite 79, ClassA, Gravity), animation 7 (walk) with the speed of
the animation set overridden by the harness to 400 (4.6875 px per tick, the full step is 307 200 raw), over the ascending ramp of k0_walker.py (cells 3..6: height byte 1..4, slope 3), the entity starting at rest
on the flat ground (T = 0) at x = 61 (key magnet-3px) and x = 62 (key magnet-4px).  The rise of the terrain under the box at the end of tick 1 is 3 px, then 4 px; everything else is the same.
What the oracle shows (out/magnet-summary.txt): with 3 px the first attempt is accepted whole (displacement 307 200 raw), with 4 px it is not (307 196 raw, the refinement of 0x80037730 stops 4 raw short) and the entity
ends the tick on the terrain either way (PosZ = T + 1 raw).  Witnesses: walker-ramp-up and walker-ramp-down (k0_walker.py E2/E3).
Run: python -B -I k0_magnet.py   (writes out/magnet-*.txt and out/magnet-*.json, prints the summary)"""
import sys, os
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import k0lib as K
import interp2
import k0_walker as WK
from k0_arrows import OUT

COLS = ['status', 'anim_t', 'anim_c', 'fz_B8', 'fnz_EC', 'fx_C4', 'fnx_E4', 'px', 'pz', 'terr_138', 'floor_134', 'ground_144', 'collz_140', 'adj_13C']
SPEED = 400          # animation-set speed of the walk (anim 7), harness override: 400 * 768 / 65536 = 4.6875 px per tick = 307 200 raw
TICKS = 6


def ramp_up(col, row):
    if col < 3:
        return None
    if col <= 6:
        return dict(h=1 + (col - 3), slope=3)
    return dict(h=4)


def run(x0, ticker=None):
    return WK.walker(cells=ramp_up, anim=7, n=TICKS, pos=(x0, 100), speed_override={7: SPEED}, ticker=ticker)


if __name__ == '__main__':
    lines = []
    for name, x0 in (('magnet-3px', 61), ('magnet-4px', 62)):
        m, s, init, rows = run(x0)
        m2, s2, init2, rows2 = run(x0, ticker=interp2.tick2)
        d = K.same_rows(rows, rows2)
        txt = K.table(rows, s, COLS, 1, TICKS, 1, title='== %s: Beannoid (anim 7, speed %d) from x = %d on flat ground, ascending ramp from column 3; interp diff %s\nstart state: %s' % (
            name, SPEED, x0, d, ' '.join('%s=%s' % (c, K.fmt(c, init[c])) for c in COLS if c in init)))
        open(os.path.join(OUT, name + '.txt'), 'w').write(txt + '\n')
        K.dump_json(os.path.join(OUT, name + '.json'), rows, dict(name=name, slot=s, init=init, speed=SPEED, x0=x0))
        prev = init
        for r in rows[:2]:
            c = r['slots'][s]
            lines.append('%s tick %d: displacement %d raw (full step %d), rise of PosZ %d raw (%.4f px), TerrainHeight %d raw, PosZ - TerrainHeight %d raw, IsOnGround %d, ForceAdjusted %d' % (
                name, r['tick'], c['px'] - prev['px'], SPEED * 768, c['pz'] - prev['pz'], (c['pz'] - prev['pz']) / 65536, c['terr_138'], c['pz'] - c['terr_138'], c['ground_144'], c['adj_13C']))
            prev = c
        lines.append('%s: two interpreters differ at: %s' % (name, d))
    txt = '\n'.join(lines)
    open(os.path.join(OUT, 'magnet-summary.txt'), 'w').write(txt + '\n')
    print(txt)
