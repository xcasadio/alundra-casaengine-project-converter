import sys
from hh import *
from t_xy import setup_xy
sys.stdout.reconfigure(encoding="utf-8")
CRATE_FLAGS, CRATE_OFF, CRATE_SIZE = 0x230180, (-12, -8, 0), (24, 16, 16)
def chain(title, crate_gravity=True):
    h = H()
    X, Y = 100 << 16, 100 << 16
    zp = 1 + 40 * U
    plat = 2
    h.add(plat, PLAT_FLAGS, PLAT_OFF, PLAT_SIZE, (X, Y, zp))
    zc = zp + 8 * U
    h.add(1, CRATE_FLAGS if crate_gravity else CRATE_FLAGS & ~0x100, CRATE_OFF, CRATE_SIZE, (X, Y, zc))
    h.add(0, HERO_FLAGS, HERO_OFF, HERO_SIZE, (X, Y, zc + 16 * U))
    print("==", title)
    for t in range(0, 9):
        if t == 2:
            h.p(plat, "forcez", 65536)
            setup_xy(h, plat, 85, 24, 65536, 0, 65536)
        h.tick()
        a, c, q = h.snap(0), h.snap(1), h.snap(plat)
        print("t%d plat x=%8d z=%8d | crate x=%8d (rel %7d) z-rel %7d ride=%s coll=%d gnd=%d ffx=%d | hero x=%8d (rel crate %7d) z-rel %7d ride=%s coll=%d gnd=%d ffx=%d ffz=%d" % (
            t, q["x"], q["z"], c["x"], c["x"]-q["x"], c["z"]-q["z"]-8*U, c["ride"], c["coll"], c["ground"], c["ffx"], a["x"], a["x"]-c["x"], a["z"]-c["z"]-16*U, a["ride"], a["coll"], a["ground"], a["ffx"], a["ffz"]))
chain("hero on gravity crate on platform rising + moving east")
chain("same, crate WITHOUT gravity bit (cannot ride)", crate_gravity=False)
