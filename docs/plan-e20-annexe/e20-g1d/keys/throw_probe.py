"""E20.g1d T0 (c): throw_probe. Derived from the critique's throw_probe.py: the bomb carried standing, then THROWN with the Square (edge at tick 50), hero facing each of the 4 directions,
EVERY tick from 48 (the bomb in the lists) printed in the common format. Oracle: copy of docs/plan-e20-annexe/e20-h3/bin/run in $G1D_ORC/oracle_h3bin."""
import os
import sys
import keyrows as K
sys.path.insert(0, os.path.join(K.ORC, 'oracle_h3bin'))
import h3_game as G  # noqa: E402
import world as W  # noqa: E402


def case(face):
    m, STATS = G.make(0x23, hero_px=(200, 200))
    holdface = G.HOLD[face]

    def holds(t):
        h = 0
        if t <= 3:
            h |= holdface
        if t == 4:
            h |= G.CIRCLE
        if t == 50:
            h |= G.SQUARE
        return h
    r = G.Run(m, holds)
    rows = []

    def post(m, t):
        W.run_pass(m, 0x800399B8)
        bomb = None
        for i in range(1, 40):
            q = W.ent(i)
            if m.peeks(q + 0x10) in (2, 3) and m.peeks(q + 0x68) == 2:
                bomb = q
                break
        if bomb is None:
            return
        rows.append((t, K.fields(m, G.HERO), K.fields(m, bomb)))
    r.step(90, post=post)
    for t, h, o in rows:
        if t >= 48:
            print(K.line('throw_' + face, t, h, o))


print(K.HEADER)
for face in ('right', 'left', 'down', 'up'):
    case(face)
