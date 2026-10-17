import sys
sys.path.insert(0, ".")
from oracle_p import *
sys.stdout.reconfigure(encoding="utf-8")
# the hero jumps (impulse IZF 1280 = ForceZ 1280<<8 = 5 px/tick at the take-off tick, then the map gravity) off a platform rising at 1 px/tick
p = P()
X, Y = 100 << 16, 100 << 16
zp = 1 + 100 * U
p.add(1, PLAT_FLAGS_BIG, PLAT_OFF_BIG, PLAT_SIZE_BIG, (X, Y, zp))
p.add(0, HERO_FLAGS, HERO_OFF, HERO_SIZE, (X, Y, zp + 8 * U))
p.put(1, "forcez", 65536)
print("platform rises 1 px/tick the whole time ; hero impulse IZF=1280 at t3 (the +0xF8 word is cleared by UpdateAnimation at t4)")
for t in range(0, 12):
    if t == 3: p.put(0, "izf", 1280)
    if t == 4: p.put(0, "izf", 0)
    p.tick()
    h, c = p.snap(0), p.snap(1)
    print("t%02d plat z=%8d | hero z=%8d (rel %8d) fz=%8d ffz=%8d ride=%s coll=%d gnd=%d" % (t, c["z"], h["z"], h["z"] - c["z"] - 8 * U, h["fz"], h["ffz"], h["ride"], h["coll"], h["ground"]))
