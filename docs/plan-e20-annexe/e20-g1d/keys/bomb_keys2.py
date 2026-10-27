"""E20.g1d T0 (c): bomb_keys2. Derived from the discovery's bomb_keys2.py: same scenario (hero at (200,200) facing east, Circle at tick 4, then walk UP/DOWN/LEFT/RIGHT with the bomb
and four jumps with the Cross while walking), EVERY tick printed in the common format. Oracle: copy of docs/plan-e20-annexe/e20-h3/bin/run in $G1D_ORC/oracle_h3bin."""
import os
import sys
import keyrows as K
sys.path.insert(0, os.path.join(K.ORC, 'oracle_h3bin'))
import h3_game as G  # noqa: E402
import world as W  # noqa: E402


def holds(t):
    h = 0
    if t <= 3:
        h |= G.RIGHT
    if t == 4:
        h |= G.CIRCLE
    if 50 <= t < 75:
        h |= G.UP
    if 75 <= t < 100:
        h |= G.DOWN
    if 100 <= t < 125:
        h |= G.LEFT
    if 125 <= t < 150:
        h |= G.RIGHT
    for start, d in ((160, G.UP), (200, G.DOWN), (240, G.LEFT), (280, G.RIGHT)):
        if start <= t < start + 16:
            h |= d
        if t == start + 2:
            h |= G.CROSS
    return h


m, STATS = G.make(0x23, hero_px=(200, 200))
r = G.Run(m, holds)
rows = []


def post(m, t):
    W.run_pass(m, 0x800399B8)
    bomb = None
    for i in range(1, 40):
        p = W.ent(i)
        if m.peeks(p + 0x10) in (2, 3) and m.peeks(p + 0x68) == 2:
            bomb = p
            break
    if bomb is None:
        return
    rows.append((t, K.fields(m, G.HERO), K.fields(m, bomb)))


r.step(300, post=post)
print(K.HEADER)
for t, h, o in rows:
    print(K.line('bomb_keys2', t, h, o))
