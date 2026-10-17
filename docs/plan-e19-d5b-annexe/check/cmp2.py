"""cmp2.py - my own re-run of the 'DLL transcription vs binary' experiment, with MY harness as the binary oracle, my seeds, and extra scenario axes
(hero impulses = jumps, carrier floating at several heights incl. ground contact, hero without gravity bit)."""
import sys, random, collections
from hh import *
import dllmodel_theirs as DM
HP = 8 * U

def one(rnd, hero_jumps, low_start, hero_grav=True):
    grav = rnd.random() < 0.15
    forces = []
    for t in range(14):
        if rnd.random() < 0.35:
            forces.append((t, rnd.choice([0, 0, 32768, -32768, 65536, -65536, 131072, -131072, 3 * 65536, -3 * 65536, 12345])))
    jumps = {}
    if hero_jumps:
        for t in range(14):
            if rnd.random() < 0.12:
                jumps[t] = rnd.choice([1280, 768, 400])
    h = H()
    X, Y = 100 << 16, 100 << 16
    zp = 1 + (rnd.choice([0, 8, 40, 300]) if low_start else 300) * U
    cflags = PLAT_FLAGS | (0x100 if grav else 0)
    hf = HERO_FLAGS if hero_grav else HERO_FLAGS & ~0x100
    h.add(1, cflags, PLAT_OFF, PLAT_SIZE, (X, Y, zp))
    h.add(0, hf, HERO_OFF, HERO_SIZE, (X, Y, zp + HP))
    def mk(order, ablate=frozenset()):
        c = DM.D("carrier", False, grav, zp - 1, depth=(8 << 16) - 1, w=(48 << 16) - 1, h=(32 << 16) - 1, mx=X - (24 << 16), my=Y - (16 << 16))
        hh_ = DM.D("hero", True, hero_grav, zp + HP - 1, depth=(32 << 16) - 1, w=(21 << 16) - 1, h=(15 << 16) - 1, mx=X - (10 << 16), my=Y - (7 << 16), held=True)
        return DM.DW([hh_, c], terrain=0, order=order, ablate=ablate), hh_, c
    models = {"current": mk("current"), "proposed": mk("proposed")}
    wr = dict(forces)
    res = {o: collections.Counter() for o in models}
    first_bad = {}
    for t in range(14):
        if t in wr:
            h.p(1, "forcez", wr[t])
            for o, (w, hr, c) in models.items():
                c.forcez = wr[t]
        # hero impulse lasts one tick (IZF cleared by the animation clock at the next tick)
        izf = jumps.get(t, 0)
        h.p(0, "izf", izf)
        for o, (w, hr, c) in models.items():
            hr.izf = izf
        h.tick()
        hs, cs = h.snap(0), h.snap(1)
        b = dict(rel=hs["z"] - cs["z"] - HP, ground=hs["ground"], coll=hs["coll"], ride=1 if hs["ride"] == 1 else 0, cz=cs["z"] - 1, hz=hs["z"] - 1)
        for o, (w, hr, c) in models.items():
            w.tick()
            m = dict(rel=hr.posz - c.posz - HP, ground=hr.ground, coll=hr.coll, ride=1 if hr.ride is c else 0, cz=c.posz, hz=hr.posz)
            for k in b:
                if m[k] != b[k]:
                    res[o][k] += 1
                    first_bad.setdefault(o, (t, k, b[k], m[k]))
    return res, first_bad, dict(grav=grav, forces=forces, jumps=jumps, zp=zp)

def run(n, seed, hero_jumps, low_start, hero_grav=True, show=2):
    rnd = random.Random(seed)
    bad = collections.Counter(); tot = collections.Counter()
    shown = 0
    for i in range(n):
        res, fb, info = one(rnd, hero_jumps, low_start, hero_grav)
        for o in res:
            if res[o]: bad[o] += 1
            tot[o] += sum(res[o].values())
        if res["proposed"] and shown < show:
            shown += 1
            print("   proposed mismatch example:", fb.get("proposed"), info)
    print("n=%d seed=%d jumps=%s low=%s herograv=%s -> scenarios with mismatch: current %d, proposed %d" % (n, seed, hero_jumps, low_start, hero_grav, bad["current"], bad["proposed"]))






