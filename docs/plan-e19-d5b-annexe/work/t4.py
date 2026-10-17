import sys
sys.path.insert(0, ".")
from oracle_p import *
from t3 import setup_xy
sys.stdout.reconfigure(encoding="utf-8")
CRATE_FLAGS, CRATE_OFF, CRATE_SIZE = 0x230180, (-12, -8, 0), (24, 16, 16)     # gravity crate (bouquet / crate class)
def chain(title, order_hero_first=True, crate_gravity=True, xy=True):
    p = P()
    X, Y = 100 << 16, 100 << 16
    zp = 1 + 40 * U
    plat = 2
    p.add(plat, PLAT_FLAGS_BIG, PLAT_OFF_BIG, PLAT_SIZE_BIG, (X, Y, zp))
    zc = zp + 8 * U
    p.add(1, CRATE_FLAGS if crate_gravity else CRATE_FLAGS & ~0x100, CRATE_OFF, CRATE_SIZE, (X, Y, zc))
    p.add(0, HERO_FLAGS, HERO_OFF, HERO_SIZE, (X, Y, zc + 16 * U))
    print("==", title)
    for t in range(0, 9):
        if t == 2:
            p.put(plat, "forcez", 65536)
            if xy: setup_xy(p, plat, 85, 24, 65536, 0, 65536)
        p.tick()
        h, c, q = p.snap(0), p.snap(1), p.snap(plat)
        print("t%d plat x=%8d z=%8d | crate x=%8d (rel %7d) z-rel %7d ride=%s coll=%d gnd=%d ffx=%d | hero x=%8d (rel crate %7d) z-rel %7d ride=%s coll=%d gnd=%d ffx=%d ffz=%d" % (
            t, q["x"], q["z"], c["x"], c["x"]-q["x"], c["z"]-q["z"]-8*U, c["ride"], c["coll"], c["ground"], c["ffx"], h["x"], h["x"]-c["x"], h["z"]-c["z"]-16*U, h["ride"], h["coll"], h["ground"], h["ffx"], h["ffz"]))
chain("hero on gravity crate on platform rising + moving east")
chain("same, crate WITHOUT gravity bit (cannot ride)", crate_gravity=False)
