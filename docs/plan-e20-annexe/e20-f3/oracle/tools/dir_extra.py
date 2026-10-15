"""dir_extra.py - critic of E20.f3: the direction cases the bin report quotes but its dirtest.txt does not hold, and the four ground directions.
Hunter's bow (item 5).  usage: python -B dir_extra.py   (writes ../out/dir_extra.txt)"""
import sys, os
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import world as W
from dirtest import run, OUT

out = []
def last_arrow(lines):
    return [l for l in lines if 'arrow spawned' in l or 'arrow: dir_t' in l][-2:]

cases = [
  ('air: Cross t1, Square alone at t8, Left only from t9 (turn request ON the spawn tick)', {1: W.CROSS, 8: W.SQUARE, **{t: W.LEFT for t in range(9, 14)}}, 20, 24),
  ('air: Cross t1, Left held t5..t7, Square at t8 (turn BEFORE the press, facing already west)', {1: W.CROSS, **{t: W.LEFT for t in range(5, 14)}, 8: W.SQUARE | W.LEFT}, 20, 24),
  ('ground: facing east, Square+Down at t1', {1: W.SQUARE | W.DOWN}, 16, 24),
  ('ground: facing east, Square+Left at t1', {1: W.SQUARE | W.LEFT}, 16, 24),
  ('ground: facing east, Square+Right at t1', {1: W.SQUARE | W.RIGHT}, 16, 24),
  ('ground: facing north (dir word 16), Square alone at t1', {1: W.SQUARE}, 16, 16),
  ('ground: facing west (dir word 8), Square alone at t1', {1: W.SQUARE}, 16, 8),
  ('ground: facing south (dir word 0), Square alone at t1', {1: W.SQUARE}, 16, 0),
]
for name, holds, n, d in cases:
    lines = run(name, holds, n, direction=d)
    out.append('# ' + name)
    out += [l for l in lines[1:] if ('arrow spawned' in l) or ('arrow: dir_t' in l)][-3:]
open(os.path.join(OUT, 'dir_extra.txt'), 'w').write('\n'.join(out) + '\n')
print('\n'.join(out))
