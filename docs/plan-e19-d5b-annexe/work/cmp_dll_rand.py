"""cmp_dll_rand.py - 600 random scripted scenarios: real binary (oracle_p) vs the python transcription of the DLL, 'current' and 'proposed' orders.
Carrier floats 300 px above the terrain (no terrain contact), no-gravity (script-driven lift) 85% / gravity 15%; the script writes a new ForceZ with probability 0.35 per tick."""
import sys, random, collections
sys.path.insert(0, ".")
import cmp_dll as C
sys.stdout.reconfigure(encoding="utf-8")
from oracle_p import *
import dllmodel as DM

def run(rnd):
    grav = rnd.random() < 0.15
    forces = []
    for t in range(14):
        if rnd.random() < 0.35:
            forces.append((t, rnd.choice([0, 0, 32768, -32768, 65536, -65536, 131072, -131072, 3 * 65536, -3 * 65536, 12345, -2049 * 256])))
    # the scenario runner of cmp_dll starts the carrier 40 px up; here 300 px up : re-implement the start height through ground_start False + shift
    p = P()
    X, Y = 100 << 16, 100 << 16
    zp = 1 + 300 * U
    HP = C.HP
    cflags = PLAT_FLAGS_BIG | (0x100 if grav else 0)
    p.add(1, cflags, PLAT_OFF_BIG, PLAT_SIZE_BIG, (X, Y, zp))
    p.add(0, HERO_FLAGS, HERO_OFF, HERO_SIZE, (X, Y, zp + HP))
    def mk(order, ablate=frozenset()):
        c = DM.D("carrier", False, grav, zp - 1, depth=(8 << 16) - 1, w=(48 << 16) - 1, h=(32 << 16) - 1, mx=X - (24 << 16), my=Y - (16 << 16))
        h = DM.D("hero", True, True, zp + HP - 1, depth=(32 << 16) - 1, w=(21 << 16) - 1, h=(15 << 16) - 1, mx=X - (10 << 16), my=Y - (7 << 16), held=True)
        return DM.DW([h, c], terrain=0, order=order.split("-")[0], ablate=frozenset(order.split("-")[1:])), h, c
    models = {o: mk(o) for o in ("current", "proposed", "proposed-peek", "proposed-movefirst", "proposed-endpass")}
    wr = dict(forces)
    res = {o: collections.Counter() for o in models}
    for t in range(14):
        if t in wr:
            p.put(1, "forcez", wr[t])
            for o, (w, h, c) in models.items():
                c.forcez = wr[t]
        p.tick()
        hs, cs = p.snap(0), p.snap(1)
        b = dict(rel=hs["z"] - cs["z"] - HP, ground=hs["ground"], coll=hs["coll"], ride=1 if hs["ride"] == 1 else 0, cz=cs["z"] - 1, hz=hs["z"] - 1)
        for o, (w, h, c) in models.items():
            w.tick()
            m = dict(rel=h.posz - c.posz - HP, ground=h.ground, coll=h.coll, ride=1 if h.ride is c else 0, cz=c.posz, hz=h.posz)
            for k in b:
                if m[k] != b[k]:
                    res[o][k] += 1
    return res, grav, len(forces)

rnd = random.Random(5)
tot = {o: collections.Counter() for o in ("current", "proposed", "proposed-peek", "proposed-movefirst", "proposed-endpass")}
bad = {o: 0 for o in tot}
n = 600
for i in range(n):
    res, grav, nf = run(rnd)
    for o in tot:
        tot[o].update(res[o])
        if res[o]:
            bad[o] += 1
print("scenarios", n)
for o in tot:
    print("  %-9s scenarios with >=1 mismatching tick: %3d / %d   mismatching ticks by field: %s" % (o, bad[o], n, dict(tot[o])))
