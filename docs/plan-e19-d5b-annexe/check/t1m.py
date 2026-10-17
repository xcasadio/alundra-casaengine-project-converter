import sys
from hh import *
h = H()
X, Y = 100 << 16, 100 << 16
zp = 1
h.add(1, PLAT_FLAGS, PLAT_OFF, PLAT_SIZE, (X, Y, zp))
zh = zp + 8 * U
h.add(0, HERO_FLAGS, HERO_OFF, HERO_SIZE, (X, Y, zh))
print("hero", h.snap(0)); print("plat", h.snap(1))
for t in range(0, 14):
    if t == 3: h.p(1, "forcez", 65536)
    if t == 9: h.p(1, "forcez", 0)
    h.tick()
    a, b = h.snap(0), h.snap(1)
    print("t%02d plat z=%9d fz=%7d | hero z=%9d rel=%7d fz=%7d ffz=%7d ride=%s coll=%d gnd=%d" % (t, b['z'], b['fz'], a['z'], a['z']-b['z']-8*U, a['fz'], a['ffz'], a['ride'], a['coll'], a['ground']))
