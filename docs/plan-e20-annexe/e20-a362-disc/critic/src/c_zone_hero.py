"""c_zone_hero.py - critic: the hero's own threshold to a posed bomb (the margins of C9): bomb at (hx+DX, hy+DY), status 3 at K=5, hero idle; prints whether hp drops within 60 ticks."""
import sys
import spellworld as S
import world as W
from scen_lib import *
from scen_zone import spawn
def trial(dx, dy):
    m, STATS = mk(None, indices=(1, 2, 253))
    H = W.ent(0)
    hx, hy = m.peeks(H + 0x114) / 65536, m.peeks(H + 0x118) / 65536
    pad = W.Pad()
    b = spawn(m, H, 0, 2, hx + dx, hy + dy, 0)
    m.poke(b + 0x274, 240)
    for t in range(0, 60):
        m.tick_no = t
        W.tick(m, pad, 0, check_pad=False)
        if t == 5: m.poke(b + 0x10, 3)
    return m.peeks(H + 0x14)
for name, vals in (('east dx', [(d, 0) for d in (34.0, 34.75, 35.0, 35.25, 35.5, 36.0, 36.5)]), ('south dy', [(0, d) for d in (22.0, 23.0, 23.5, 24.0, 24.5, 25.0)]), ('north dy', [(0, -d) for d in (22.0, 23.0, 23.5, 24.0, 24.5, 25.0)]), ('west dx', [(-d, 0) for d in (34.0, 34.75, 35.0, 35.25, 35.5, 36.0, 36.5)])):
    print(name, [(v, trial(*v)) for v in vals])
