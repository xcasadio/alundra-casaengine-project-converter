import sys
from hh import *
sys.stdout.reconfigure(encoding="utf-8")
PLAT_SMALL = 0x0360C0

def run(title, script, n, hero_gravity=True, plat_h=8):
    h = H()
    X, Y = 100 << 16, 100 << 16
    zp = 1 + 40 * U
    h.add(1, PLAT_FLAGS, PLAT_OFF, PLAT_SIZE, (X, Y, zp))
    zh = zp + plat_h * U
    h.add(0, HERO_FLAGS if hero_gravity else HERO_FLAGS & ~0x100, HERO_OFF, HERO_SIZE, (X, Y, zh))
    print("==", title)
    for t in range(n):
        script(h, t)
        h.tick()
        a, c = h.snap(0), h.snap(1)
        print("t%02d  plat z=%9d fz=%8d ffz=%8d | hero z=%9d (rel %8d) fz=%8d ffz=%8d ride=%s coll=%d gnd=%d" % (t, c["z"], c["fz"], c["ffz"], a["z"], a["z"]-c["z"]-8*U, a["fz"], a["ffz"], a["ride"], a["coll"], a["ground"]))

def desc(h, t):
    if t == 2: h.p(1, "forcez", -65536)
    if t == 7: h.p(1, "forcez", 0)
run("descending 1 px/tick from t2, stop at t7, hero on it, platform 40 px above ground", desc, 11)
def desc2(h, t):
    if t == 2: h.p(1, "forcez", -32768)
    if t == 7: h.p(1, "forcez", -131072)
    if t == 10: h.p(1, "forcez", 0)
run("descending 0.5 then 2 px/tick", desc2, 13)
