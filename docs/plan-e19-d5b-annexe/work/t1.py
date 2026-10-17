import sys
sys.path.insert(0, ".")
from oracle_p import *
sys.stdout.reconfigure(encoding="utf-8")
p = P()
X, Y = 100 << 16, 100 << 16
zp = 1                                  # platform on terrain 0 (binary rest = T + 1)
p.add(1, PLAT_FLAGS_BIG, PLAT_OFF_BIG, PLAT_SIZE_BIG, (X, Y, zp))
zh = zp + 8 * U                         # hero stands on the platform top + 1
p.add(0, HERO_FLAGS, HERO_OFF, HERO_SIZE, (X, Y, zh))
print("setup hero", p.snap(0)); print("setup plat", p.snap(1))
def show(t):
    h, c = p.snap(0), p.snap(1)
    print("t%02d  plat z=%9d fz=%8d ffz=%8d | hero z=%9d (rel %8d) fz=%8d ffz=%8d ride=%s coll=%d gnd=%d" % (t, c["z"], c["fz"], c["ffz"], h["z"], h["z"]-c["z"]-8*U, h["fz"], h["ffz"], h["ride"], h["coll"], h["ground"]))
for t in range(0, 14):
    if t == 3: p.put(1, "forcez", 65536)          # script: Fly +1 px / tick
    if t == 9: p.put(1, "forcez", 0)              # script: stop
    p.tick(); show(t)
