from hh import *
import dllmodel_theirs as DM
HP = 8 * U
def run(gap_px, rise):
    h = H(); X = Y = 100 << 16; zp = 1 + 300 * U
    h.add(1, PLAT_FLAGS, PLAT_OFF, PLAT_SIZE, (X, Y, zp))
    h.add(0, HERO_FLAGS, HERO_OFF, HERO_SIZE, (X, Y, zp + HP + int(gap_px * U)))
    c = DM.D("carrier", False, False, zp - 1, depth=(8<<16)-1, w=(48<<16)-1, h=(32<<16)-1, mx=X-(24<<16), my=Y-(16<<16))
    hr = DM.D("hero", True, True, zp + HP + int(gap_px*U) - 1, depth=(32<<16)-1, w=(21<<16)-1, h=(15<<16)-1, mx=X-(10<<16), my=Y-(7<<16), held=True)
    w = DM.DW([hr, c], terrain=0, order="proposed")
    h.p(1, "forcez", rise); c.forcez = rise
    print("--- hero starts %.2f px above rest, carrier rises %d (%.2f px/tick); hero air state (gravity %d/tick)" % (gap_px, rise, rise / 65536, 128 << 8))
    for t in range(6):
        h.tick(); w.tick()
        hs, cs = h.snap(0), h.snap(1)
        print("t%d BIN: carrier dz=%7d hero rel=%8d coll=%d gnd=%d ride=%s | PROPOSED-MODEL(M1-M4): hero rel=%8d coll=%d gnd=%d" % (t, cs["z"] - (zp if t == 0 else getattr(run, "prev", zp)), hs["z"] - cs["z"] - HP, hs["coll"], hs["ground"], hs["ride"], hr.posz - c.posz - HP, hr.coll, hr.ground))
        run.prev = cs["z"]
run(1.0, 196608)
run(0.25, 65536)
