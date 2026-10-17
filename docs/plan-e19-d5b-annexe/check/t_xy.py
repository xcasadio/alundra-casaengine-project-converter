import sys
from hh import *
sys.stdout.reconfigure(encoding="utf-8")
REC = 0x80170000
def setup_xy(h, idx, speed, dirn, tx, ty, step):
    e = h.e
    e.wh(REC + 8, speed); e.wb(REC + 0xD, 0)
    h.p(idx, "x9c", 0) if False else e.ww(ent(idx) + 0x9C, REC)
    e.ww(ent(idx) + 0xF4, speed); e.ww(ent(idx) + 0x8C, dirn); e.ww(ent(idx) + 0x94, dirn); e.ww(ent(idx) + 0xF0, 0)
    e.ww(ent(idx) + 0xBC, tx & M32); e.ww(ent(idx) + 0xC0, ty & M32); e.ww(ent(idx) + 0xD4, step); e.ww(ent(idx) + 0xD8, step)

h = H()
X, Y = 100 << 16, 100 << 16
zp = 1 + 40 * U
h.add(1, PLAT_FLAGS, PLAT_OFF, PLAT_SIZE, (X, Y, zp))
h.add(0, HERO_FLAGS, HERO_OFF, HERO_SIZE, (X, Y, zp + 8 * U))
for t in range(12):
    if t == 3: setup_xy(h, 1, 85, 24, 65536, 0, 65536)
    if t == 8: setup_xy(h, 1, 0, 24, 0, 0, 65536)
    h.tick()
    a, c = h.snap(0), h.snap(1)
    print("t%02d plat x=%9d afx=%7d ffx=%7d | hero x=%9d (rel %8d) afx=%7d ffx=%7d  z-rel %d ride=%s coll=%d gnd=%d" % (t, c["x"], c["adjx"], c["ffx"], a["x"], a["x"]-c["x"], a["adjx"], a["ffx"], a["z"]-c["z"]-8*U, a["ride"], a["coll"], a["ground"]))
