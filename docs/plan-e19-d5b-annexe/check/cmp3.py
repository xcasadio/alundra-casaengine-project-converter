import sys
from hh import *
import dllmodel_theirs as DM
HP = 8 * U
def scen(forces, jumps, zp, grav=False, hero_grav=True, order_list=("current", "proposed")):
    h = H()
    X, Y = 100 << 16, 100 << 16
    cflags = PLAT_FLAGS | (0x100 if grav else 0)
    h.add(1, cflags, PLAT_OFF, PLAT_SIZE, (X, Y, zp))
    h.add(0, HERO_FLAGS if hero_grav else HERO_FLAGS & ~0x100, HERO_OFF, HERO_SIZE, (X, Y, zp + HP))
    models = {}
    for o in order_list:
        c = DM.D("carrier", False, grav, zp - 1, depth=(8 << 16) - 1, w=(48 << 16) - 1, h=(32 << 16) - 1, mx=X - (24 << 16), my=Y - (16 << 16))
        hr = DM.D("hero", True, hero_grav, zp + HP - 1, depth=(32 << 16) - 1, w=(21 << 16) - 1, h=(15 << 16) - 1, mx=X - (10 << 16), my=Y - (7 << 16), held=True)
        models[o] = (DM.DW([hr, c], terrain=0, order=o), hr, c)
    wr = dict(forces)
    for t in range(14):
        if t in wr:
            h.p(1, "forcez", wr[t])
            for o, (w, hr, c) in models.items(): c.forcez = wr[t]
        izf = jumps.get(t, 0)
        h.p(0, "izf", izf)
        for o, (w, hr, c) in models.items(): hr.izf = izf
        h.tick()
        hs, cs = h.snap(0), h.snap(1)
        line = "t%02d izf=%4d F=%7d | BIN carrier z=%9d hero rel=%8d fz=%7d ffz=%7d ride=%s coll=%d gnd=%d" % (t, izf, cs['fz'], cs['z'], hs['z']-cs['z']-HP, hs['fz'], hs['ffz'], hs['ride'], hs['coll'], hs['ground'])
        for o, (w, hr, c) in models.items():
            w.tick()
            line += " | %s rel=%8d fz=%7d ffz=%7d ride=%d coll=%d gnd=%d" % (o[:4], hr.posz-c.posz-HP, hr.forcez, hr.finalz, 1 if hr.ride is c else 0, hr.coll, hr.ground)
        print(line)
print("--- ex2: forces [(4,32768),(5,0),(11,131072)] jumps {5:768, 7:400}")
scen([(4, 32768), (5, 0), (11, 131072)], {5: 768, 7: 400}, 19660801)
print("--- ex1: forces [(0,-65536),(3,-65536),(6,0),(7,196608)] jumps {2:768}")
scen([(0, -65536), (3, -65536), (6, 0), (7, 196608)], {2: 768}, 19660801)
