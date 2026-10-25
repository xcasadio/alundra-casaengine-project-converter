"""pb_hero_wall.py - how close to a wall (column 5, x >= 120) can the hero of the binary stand? The hero walks east with the pad RIGHT; we print his x when he stops.
Then, for a hero standing at his closest position, the point-blank shot of each projectile (the real case of a player pressing the weapon button against a wall)."""
import os
import sys

HERE = os.path.join(os.path.dirname(os.path.abspath(__file__)), 'f4f6bin')
sys.path.insert(0, HERE)
import fw
import world as W

OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), 'out')
os.makedirs(OUT, exist_ok=True)
lines = []
for start in (100, 104, 108):
    res = fw.run(dict(item=None, n=80, holds={t: W.RIGHT for t in range(1, 81)}, after=0, hero_px=(start, 100), direction=24, cells=fw.wall_col(5), targets=[], seed=0x12345678))
    xs = [(r['tick'], r['slots'][0]['px'] / 65536, r['slots'][0]['fx'] / 65536) for r in res['rows'] if 0 in r['slots']]
    lines.append('hero walking east from %d: ' % start + ' '.join('%d:%.3f' % (t, x) for t, x, f in xs[::8]) + '  final x=%.3f' % xs[-1][1])
    lines.append('  last 6 ticks: ' + ' '.join('%d:%.3f(f=%.2f)' % (t, x, f) for t, x, f in xs[-6:]))
open(os.path.join(OUT, 'pb_hero_wall.txt'), 'w').write('\n'.join(lines) + '\n')
print('\n'.join(lines))
