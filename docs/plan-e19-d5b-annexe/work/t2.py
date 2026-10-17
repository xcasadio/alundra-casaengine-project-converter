import sys
sys.path.insert(0, ".")
from oracle_p import *
sys.stdout.reconfigure(encoding="utf-8")
def run(title, script, n, hero_gravity=True, plat_h=8):
    p = P()
    X, Y = 100 << 16, 100 << 16
    zp = 1 + 40 * U                     # platform floating 40 px above the terrain
    p.add(1, PLAT_FLAGS_BIG, PLAT_OFF_BIG, PLAT_SIZE_BIG, (X, Y, zp))
    zh = zp + plat_h * U
    p.add(0, HERO_FLAGS if hero_gravity else HERO_FLAGS & ~0x100, HERO_OFF, HERO_SIZE, (X, Y, zh))
    print("==", title)
    for t in range(n):
        script(p, t)
        p.tick()
        h, c = p.snap(0), p.snap(1)
        print("t%02d  plat z=%9d fz=%8d ffz=%8d | hero z=%9d (rel %8d) fz=%8d ffz=%8d ride=%s coll=%d gnd=%d" % (t, c["z"], c["fz"], c["ffz"], h["z"], h["z"]-c["z"]-8*U, h["fz"], h["ffz"], h["ride"], h["coll"], h["ground"]))
def desc(p, t):
    if t == 2: p.put(1, "forcez", -65536)
    if t == 7: p.put(1, "forcez", 0)
run("descending 1 px/tick from t2, stop at t7, hero on it, platform 40 px above ground", desc, 11)
def desc2(p, t):
    if t == 2: p.put(1, "forcez", -32768)
    if t == 7: p.put(1, "forcez", -131072)
    if t == 10: p.put(1, "forcez", 0)
run("descending 0.5 then 2 px/tick", desc2, 13)
