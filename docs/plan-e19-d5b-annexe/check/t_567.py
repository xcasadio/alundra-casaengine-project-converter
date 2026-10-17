import sys
from hh import *
sys.stdout.reconfigure(encoding="utf-8")
PLAT_SMALL = 0x0360C0
CH = dict(flags=PLAT_SMALL, off=(-12, -8, 0), size=(24, 16, 16))
def run(title, hero_x_px, script, n=9):
    h = H(cell_h=lambda c, r: 0 if (c == 4 and r == 6) else 1)
    h.add(1, CH["flags"], CH["off"], CH["size"], (108 << 16, 104 << 16, 1))
    h.add(0, HERO_FLAGS, HERO_OFF, HERO_SIZE, (hero_x_px << 16, 104 << 16, (1 << 20) + 1))
    print("==", title)
    for t in range(n):
        script(h, t)
        h.tick()
        a, c = h.snap(0), h.snap(1)
        print("t%d block z=%8d | hero z=%8d (rel %7d) terrain-under-hero=%8d ride=%s coll=%d gnd=%d" % (t, c["z"], a["z"], a["z"] - c["z"] - 16 * U, a["terr"], a["ride"], a["coll"], a["ground"]))
def rise(h, t):
    if t == 2: h.p(1, "forcez", 65536)
    if t == 6: h.p(1, "forcez", 0)
run("dock half (x=120)", 120, rise)
run("hollow (x=108)", 108, rise)
def run2(title, hero_x_px, n=8):
    h = H(cell_h=lambda c, r: 0 if (c == 4 and r == 6) else 1)
    h.add(1, CH["flags"], CH["off"], CH["size"], (108 << 16, 104 << 16, 1 + 8 * U))
    h.add(0, HERO_FLAGS, HERO_OFF, HERO_SIZE, (hero_x_px << 16, 104 << 16, 1 + 8 * U + 16 * U))
    print("==", title)
    for t in range(n):
        if t == 2: h.p(1, "forcez", -65536)
        h.tick()
        a, c = h.snap(0), h.snap(1)
        print("t%d block z=%8d | hero z=%8d (rel %7d) terrain-under-hero=%8d ride=%s coll=%d gnd=%d" % (t, c["z"], a["z"], a["z"] - c["z"] - 16 * U, a["terr"], a["ride"], a["coll"], a["ground"]))
run2("desc dock half", 120, 14)
run2("desc hollow", 108)

# t6 jump off rising platform
h = H()
X, Y = 100 << 16, 100 << 16
zp = 1 + 100 * U
h.add(1, PLAT_FLAGS, PLAT_OFF, PLAT_SIZE, (X, Y, zp))
h.add(0, HERO_FLAGS, HERO_OFF, HERO_SIZE, (X, Y, zp + 8 * U))
h.p(1, "forcez", 65536)
print("== t6")
for t in range(0, 12):
    if t == 3: h.p(0, "izf", 1280)
    if t == 4: h.p(0, "izf", 0)
    h.tick()
    a, c = h.snap(0), h.snap(1)
    print("t%02d plat z=%8d | hero z=%8d (rel %8d) fz=%8d ffz=%8d ride=%s coll=%d gnd=%d" % (t, c["z"], a["z"], a["z"] - c["z"] - 8 * U, a["fz"], a["ffz"], a["ride"], a["coll"], a["ground"]))
# t7 teleport
h = H()
h.add(1, PLAT_SMALL, (-12, -8, 0), (24, 16, 16), (X, Y, 50 * U + 1))
h.add(0, HERO_FLAGS, HERO_OFF, HERO_SIZE, (X, Y, 66 * U + 1))
for t in range(3):
    h.tick()
print("== t7 settled:", h.snap(0)["z"], h.snap(1)["z"], "ride", h.snap(0)["ride"])
h.p(1, "posz", 18 * U + 1)
for t in range(1, 16):
    h.tick()
    a, c = h.snap(0), h.snap(1)
    print("tick %2d obj z=%8d | hero z=%8d (DLL conv. %8d) fz=%8d ride=%s coll=%d gnd=%d" % (t, c["z"], a["z"], a["z"] - 1, a["fz"], a["ride"], a["coll"], a["ground"]))
