from hh import *
h = H()
X, Y = 100 << 16, 100 << 16
# 24x16x16 object with gravity bit on the terrain (T=0 -> rest at 1), hero on its top
OBJ = 0x0360C0 | 0x100
h.add(1, OBJ, (-12, -8, 0), (24, 16, 16), (X, Y, 1))
h.add(0, HERO_FLAGS, HERO_OFF, HERO_SIZE, (X, Y, 1 + 16 * U))
print("start", h.snap(0), h.snap(1))
for t in range(1, 8):
    h.tick()
    a, c = h.snap(0), h.snap(1)
    print("t%d obj z=%d fz=%d ffz=%d coll=%d gnd=%d | hero z=%d fz=%d ffz=%d coll=%d gnd=%d ride=%s" % (t, c['z'], c['fz'], c['ffz'], c['coll'], c['ground'], a['z'], a['fz'], a['ffz'], a['coll'], a['ground'], a['ride']))
