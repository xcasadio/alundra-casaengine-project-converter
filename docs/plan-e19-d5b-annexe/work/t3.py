import sys
sys.path.insert(0, ".")
from oracle_p import *
sys.stdout.reconfigure(encoding="utf-8")
REC = 0x80430000
def setup_xy(p, idx, speed, dirn, target_x, target_y, step):
    """make the force pass of entity idx take the XY target forces we write (caches equal -> 0x80036614 does not recompute)"""
    m = p.m
    m.wh(REC + 8, speed)          # lhu 8(rec) = speed
    m.wb(REC + 0xD, 0)            # accel nibble 0
    p.ww(idx, 0x9C, REC)
    p.ww(idx, 0xF4, speed)        # cached speed
    p.ww(idx, 0x8C, dirn); p.ww(idx, 0x94, dirn)   # direction and its cache
    p.ww(idx, 0xF0, 0)            # cached accel
    p.ww(idx, 0xBC, target_x); p.ww(idx, 0xC0, target_y)
    p.ww(idx, 0xD4, step); p.ww(idx, 0xD8, step)
p = P()
X, Y = 100 << 16, 100 << 16
zp = 1 + 40 * U
p.add(1, PLAT_FLAGS_BIG, PLAT_OFF_BIG, PLAT_SIZE_BIG, (X, Y, zp))
p.add(0, HERO_FLAGS, HERO_OFF, HERO_SIZE, (X, Y, zp + 8 * U))
def show(t):
    h, c = p.snap(0), p.snap(1)
    print("t%02d plat x=%9d afx=%7d ffx=%7d | hero x=%9d (rel %8d) afx=%7d ffx=%7d  z-rel %d ride=%s coll=%d gnd=%d" % (t, c["x"], c["afx"], c["ffx"], h["x"], h["x"]-c["x"], h["afx"], h["ffx"], h["z"]-c["z"]-8*U, h["ride"], h["coll"], h["ground"]))
for t in range(0, 12):
    if t == 3: setup_xy(p, 1, 85, 24, 65536, 0, 65536)
    if t == 8: setup_xy(p, 1, 0, 24, 0, 0, 65536)
    p.tick(); show(t)
