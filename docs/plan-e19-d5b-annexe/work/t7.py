import sys
sys.path.insert(0, ".")
from oracle_p import *
sys.stdout.reconfigure(encoding="utf-8")
# the "teleport" case of the UH-16 text: a 24 x 16 x 16 object at 50 px (top 66 px), hero on it, no gravity on the object; between two ticks the object's PosZ drops by 32 px (script 0x64 / 0x65 write)
p = P()
X, Y = 100 << 16, 100 << 16
p.add(1, PLAT_FLAGS_SMALL, (-12, -8, 0), (24, 16, 16), (X, Y, 50 * U + 1))
p.add(0, HERO_FLAGS, HERO_OFF, HERO_SIZE, (X, Y, 66 * U + 1))
for t in range(3):
    p.tick()
print("settled:", p.snap(0)["z"], p.snap(1)["z"], "ride", p.snap(0)["ride"])
p.put(1, "posz", 18 * U + 1)                 # the teleport (a script write); ModdedZ is refreshed by the pre-pass of P
for t in range(1, 16):
    p.tick()
    h, c = p.snap(0), p.snap(1)
    print("tick %2d obj z=%8d | hero z=%8d (DLL conv. %8d) fz=%8d ride=%s coll=%d gnd=%d" % (t, c["z"], h["z"], h["z"] - 1, h["fz"], h["ride"], h["coll"], h["ground"]))
