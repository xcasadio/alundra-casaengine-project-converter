import sc_misc as SCM
import h3_game as G
from h3_game import *
def cells(col, row):
    if col >= 10:
        return (0, 4, 0, 0)
    return (0, 0, 0, 0)
m, STATS = G.make(0x23, hero_px=(200, 200), cells=cells)
r = Run(m, SCM.basic_holds(extra=lambda t: G.RIGHT if t > 45 else 0))
for t in range(1, 160):
    r.step(1)
    if t % 10 == 0 or t in (46, 47, 48):
        print(t, 'x=%.1f' % (m.peeks(HERO + 0x114) / 65536), 'slope18C', m.peeks(HERO + 0x18C), 'ta=%#x' % m.peeks(HERO + 0x88), 'c2c=%#x' % m.peek(HERO + 0x2C), 'inuse=%#x' % m.peeks(0x80127164))
print([j for j in r.js if j[1] != 'sound'][:5])
