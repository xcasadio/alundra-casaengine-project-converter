"""validate.py - zmodel.py against the REAL physics pass (zsim.py) on random single-entity scenarios + random handler triples, then mutation checks."""
import sys, os, random
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from zsim import Sim, ent, F, M32, s32, U
import zmodel as Z
sys.stdout.reconfigure(encoding="utf-8")

PAIRS = [(128, 4096), (3, 256), (128, 512), (50, 100), (200, 3000), (1, 1), (128, 128)]
REC = 0x80420000


def scenario(rnd):
    hb = rnd.choice([0, 0, 1, 2, 5, 17, 30, 40])
    G, V = rnd.choice(PAIRS)
    gravity = rnd.random() < 0.6
    subject = rnd.random() < 0.5
    modz = rnd.choice([0, 0, 8, 3, -5]) * U
    sz = rnd.choice([0, 0, 16, 32])
    T = hb * 16 * U
    mode = rnd.random()
    if mode < 0.2:
        posz = T + 1 - modz                                   # at rest
    elif mode < 0.35:
        posz = rnd.randrange(1790, 1919) * U + rnd.randrange(0, U)    # near the 1920 px cap
    else:
        posz = T + 1 - modz + rnd.randrange(0, 120 * U)
    force0 = rnd.choice([0, 0, rnd.randrange(-U, U), -(G << 8) * rnd.randrange(0, 12)])
    n = 40
    acts = []
    for t in range(n):
        r = rnd.random()
        if r < 0.15:
            acts.append(("1B", rnd.choice([0, 128, 255, rnd.randrange(0, 65536)])))
        elif r < 0.25:
            acts.append(("grav", None))
        elif r < 0.30:
            acts.append(("posz", T + 1 - modz + rnd.randrange(0, 60 * U)))
        elif r < 0.36:
            acts.append(("izf", rnd.choice([0x8000, 256, -256 & 0xFFFF, rnd.randrange(1, 4000), 0xFF00])))
        else:
            acts.append((None, None))
    return dict(hb=hb, G=G, V=V, gravity=gravity, subject=subject, modz=modz, sz=sz, T=T, posz=posz, force0=force0, acts=acts)


def run_one(sc, mut=None, verbose=False):
    s = Sim(gravity=sc["G"], visc=sc["V"], cells=lambda c, r: (sc["hb"], 0, 0, 0))
    fl = (0x100 if sc["gravity"] else 0) | (0x80 if sc["subject"] else 0)
    s.add(1, fl, off=(0, 0, sc["modz"] // U), size=(0, 0, sc["sz"]), pos=(100 << 16, 100 << 16, sc["posz"]))
    s.put(1, "forcez", sc["force0"])
    s.end_pass(ent(1))
    e = Z.Ent(posz=sc["posz"], forcez=sc["force0"], gravity=sc["gravity"], modz=sc["modz"], depth=(sc["sz"] << 16) - 1 if sc["sz"] else 0)
    for t, (a, v) in enumerate(sc["acts"]):
        if a == "1B":
            s.put(1, "forcez", s32(((v ^ 0x8000) - 0x8000) << 8)); Z.op1B(e, v & 0xFF, v >> 8)
        elif a == "grav":
            s.put(1, "flags", s.get(1, "flags") ^ 0x100); e.gravity = not e.gravity
        elif a == "posz":
            s.put(1, "posz", v); e.posz = v
        elif a == "izf":
            s.put(1, "izf", v); e.izf = s32(v if v < 0x8000 or v == 0x8000 else v) if v < 0x80000000 else v
            e.izf = s32(v)
        s.tick(rebuild=False)
        s.put(1, "izf", 0)
        Z.physics(e, sc["T"], sc["G"], sc["V"], mut)
        q = s.snap(1)
        got = (q["posz"], q["forcez"], q["finalz"], q["coll"], q["ground"], q["floor"], q["terrain"])
        exp = (e.posz, e.forcez, e.finalz, e.coll, e.ground, e.floor, e.terrain)
        if got != exp:
            return t, got, exp
    return None


def main():
    rnd = random.Random(20261006)
    N = int(sys.argv[1]) if len(sys.argv) > 1 else 300
    bad = 0
    scs = [scenario(rnd) for _ in range(N)]
    # directed: PosZ = T + j*|F| with a constant force and no gravity (the equality of the landing test, PosZ + F == T), 1-unit neighbours
    for fz in (-32768, -65536, -1, -4096):
        for j in range(1, 6):
            for dj in (-1, 0, 1):
                T = 3 * 16 * U
                scs.append(dict(hb=3, G=128, V=4096, gravity=False, subject=False, modz=0, sz=0, T=T, posz=T + j * (-fz) + dj,
                                force0=fz, acts=[(None, None)] * 12))
    N = len(scs)
    for i, sc in enumerate(scs):
        r = run_one(sc)
        if r:
            bad += 1
            if bad <= 5:
                print("MISMATCH case %d tick %d: emu %s model %s | %s" % (i, r[0], r[1], r[2], {k: v for k, v in sc.items() if k != "acts"}))
    print("physics: %d random scenarios x 40 ticks, mismatching: %d" % (N, bad))
    for mut in ("one_sided", "strict_landing", "ground_modded"):
        caught = sum(1 for sc in scs[:N] if run_one(sc, {mut}))
        print("  mutation %-15s caught in %d of %d scenarios" % (mut, caught, N))

    # handler triples: real handlers vs model
    from zsim import Sim as S2
    rnd = random.Random(7)
    badh = 0
    for k in range(3000):
        s = S2(gravity=128, visc=4096)
        P = s.add(1, 0x100, pos=(100 << 16, 100 << 16, 0))
        for i in range(0x20): s.mem.wb(REC + i, 0)
        h = rnd.randrange(0, 256)
        s.mem.wb(REC + 9, h); s.mem.ww(P + F["record"], REC)
        posz0 = rnd.randrange(-50 * U, 300 * U); posz1 = posz0 + rnd.choice([0, rnd.randrange(-300 * U, 300 * U), rnd.randrange(-3, 3)])
        fz = rnd.choice([0, rnd.randrange(-U, U), 32768, -32768])
        lim = rnd.randrange(0, 65536) if rnd.random() < .3 else rnd.randrange(0, 300)
        # 0x20
        s.put(1, "posz", posz0); s.load_program(1, bytes([0x20, lim & 0xFF, lim >> 8, 0xFF])); t1 = s.run_script(1)
        e = Z.Ent(posz=posz0); r1 = Z.op20(e, 1, lim & 0xFF, lim >> 8)
        s.put(1, "posz", posz1); t2 = s.run_script(1); e.posz = posz1; r2 = Z.op20(e, 1, lim & 0xFF, lim >> 8)
        if (t1[0][2], t2[0][2]) != (r1, r2): badh += 1; print("0x20 mismatch", posz0, posz1, lim, t1, t2, r1, r2)
        # 0x22
        s2 = S2(gravity=128, visc=4096); P2 = s2.add(1, 0x100, pos=(100 << 16, 100 << 16, posz0))
        for i in range(0x20): s2.mem.wb(REC + i, 0)
        s2.mem.wb(REC + 9, h); s2.mem.ww(P2 + F["record"], REC)
        s2.load_program(1, bytes([0x22, 0xFF])); u1 = s2.run_script(1)
        e = Z.Ent(posz=posz0, forcez=fz); q1 = Z.op22(e, 1, h)
        tgt = rnd.choice([h << 19, posz0, (h << 19) + rnd.randrange(-70000, 70000)])
        # move to a position around the target
        pz = tgt if rnd.random() < .2 else tgt + rnd.randrange(-3 * U, 3 * U)
        s2.put(1, "posz", pz); s2.put(1, "forcez", fz); e.posz = pz; e.forcez = fz
        u2 = s2.run_script(1); q2 = Z.op22(e, 1, h)
        if (u1[0][2], u2[0][2], s2.get(1, "forcez")) != (q1, q2, e.forcez): badh += 1; print("0x22 mismatch", h, pz, fz, u1, u2, q1, q2, s2.get(1, "forcez"), e.forcez)
    print("handlers: 3000 random 0x20 and 0x22 call pairs, mismatching: %d" % badh)


if __name__ == "__main__":
    main()
