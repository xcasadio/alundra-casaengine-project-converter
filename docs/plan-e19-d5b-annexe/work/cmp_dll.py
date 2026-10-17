"""cmp_dll.py - the REAL binary (oracle_p: hero slot 0 + a carrier, rider copy real) against the python transcription of the DLL, 'current' and 'proposed' orders.
Per scenario: scripted ForceZ of the carrier; compared per tick: hero Z relative to the carrier top, IsOnGround, CollidedWithEntityZ, hero riding the carrier."""
import sys, random, collections
sys.path.insert(0, ".")
from oracle_p import *
import dllmodel as DM
sys.stdout.reconfigure(encoding="utf-8")

HP = 8 * U      # carrier box height (Plateforme flottante (grande): 48 x 32 x 8)

def run_scenario(forces, carrier_gravity=False, ground_start=False, G=128, V=4096):
    """forces: list of (tick, value) writes of the carrier's ForceZ (binary: +0xB8), applied before the tick (the script phase)"""
    p = P(gravity=G, visc=V)
    X, Y = 100 << 16, 100 << 16
    zp = (1 if ground_start else 1 + 40 * U)
    cflags = PLAT_FLAGS_BIG | (0x100 if carrier_gravity else 0)
    p.add(1, cflags, PLAT_OFF_BIG, PLAT_SIZE_BIG, (X, Y, zp))
    p.add(0, HERO_FLAGS, HERO_OFF, HERO_SIZE, (X, Y, zp + HP))
    # the DLL models, same start (convention - 1)
    def mk(order):
        c = DM.D("carrier", False, carrier_gravity, zp - 1, depth=(8 << 16) - 1, w=(48 << 16) - 1, h=(32 << 16) - 1, mx=X - (24 << 16), my=Y - (16 << 16))
        h = DM.D("hero", True, True, zp + HP - 1, depth=(32 << 16) - 1, w=(21 << 16) - 1, h=(15 << 16) - 1, mx=X - (10 << 16), my=Y - (7 << 16), held=True)
        return DM.DW([h, c], terrain=0, G=G, V=V, order=order), h, c
    models = {o: mk(o) for o in ("current", "proposed")}
    wr = dict(forces)
    rows = []
    n = 14
    for t in range(n):
        if t in wr:
            p.put(1, "forcez", wr[t])
            for o, (w, h, c) in models.items():
                c.forcez = wr[t]
        p.tick()
        hs, cs = p.snap(0), p.snap(1)
        b = dict(rel=hs["z"] - cs["z"] - HP, ground=hs["ground"], coll=hs["coll"], ride=1 if hs["ride"] == 1 else 0, cz=cs["z"] - 1, hz=hs["z"] - 1)
        out = {"bin": b}
        for o, (w, h, c) in models.items():
            w.tick()
            out[o] = dict(rel=h.posz - c.posz - HP, ground=h.ground, coll=h.coll, ride=1 if h.ride is c else 0, cz=c.posz, hz=h.posz)
        rows.append(out)
    return rows

def compare(rows, key="bin"):
    res = {}
    for o in ("current", "proposed"):
        bad = collections.Counter()
        for r in rows:
            for k in ("rel", "ground", "coll", "ride", "cz"):
                if r[o][k] != r["bin"][k]:
                    bad[k] += 1
        res[o] = bad
    return res

if __name__ == "__main__":
    scen = {
        "rise 1px/tick t3..t8, stop t9": ([(3, 65536), (9, 0)], False, False),
        "descend 1px/tick t3..t8, stop t9": ([(3, -65536), (9, 0)], False, False),
        "rise 0.5 then 2 then descend 1": ([(2, 32768), (5, 131072), (8, -65536), (11, 0)], False, False),
        "gravity carrier at rest on the ground (UH-18)": ([], True, True),
        "gravity carrier at rest, script writes 1B +1px (eaten by the decay)": ([(3, 65536)], True, True),
    }
    for name, (f, g, gs) in scen.items():
        rows = run_scenario(f, g, gs)
        cmpr = compare(rows)
        print("==", name)
        print("   mismatching ticks (of %d) vs the binary:  current %s   proposed %s" % (len(rows), dict(cmpr["current"]) or "none", dict(cmpr["proposed"]) or "none"))
