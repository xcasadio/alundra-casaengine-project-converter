"""vec_xy.py - XY (and Z) carrier / passenger vectors: hero + carrier (+ a crate on the carrier, or the hero on the crate), open flat field, planned XY forces written as in t3.py.
Compares pmodel.World(xy=True) with the real pass: FinalForceX/Y (+0xE4/+0xE8), PosX/PosY, Z fields.
The XY step of the model is the open-field one (Pos += FinalForce); the real ComputeXY 0x80037730 is run for real, so any collision the real code finds shows as a mismatch.
Usage: python -B vec_xy.py [n] [seed]"""
import sys, os, random, collections
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
sys.path.insert(0, os.path.join(HERE, "h2tools"))
import oracle_p as OP
from oracle_p import P, U, M32, s32, ent
import pmodel as PM

REC = 0x80430000


def setup_xy(p, idx, speed, dirn, tx, ty, step):
    m = p.m
    m.wh(REC + 8, speed)
    m.wb(REC + 0xD, 0)
    p.ww(idx, 0x9C, REC)
    p.ww(idx, 0xF4, speed)
    p.ww(idx, 0x8C, dirn)
    p.ww(idx, 0x94, dirn)
    p.ww(idx, 0xF0, 0)
    p.ww(idx, 0xBC, tx)
    p.ww(idx, 0xC0, ty)
    p.ww(idx, 0xD4, step)
    p.ww(idx, 0xD8, step)
    p.ww(idx, 0xC4, 0)
    p.ww(idx, 0xC8, 0)


def gen(rnd):
    n = rnd.choice([2, 3])
    kinds = ["hero", "carrier", "crate"][:n]
    # layouts: hero on carrier ; hero on crate on carrier ; crate on carrier + hero beside
    layout = rnd.choice(["chain", "chain", "crate_on_carrier"]) if n == 3 else "hero_on_carrier"
    X, Y = 100 << 16, 100 << 16
    zc = 1 + 40 * U
    ents = []
    # slot order: hero 0, then the crate 1 (if any) and the carrier last, or carrier first
    order = rnd.choice(["crate_first", "carrier_first"]) if n == 3 else "carrier_first"
    plat = dict(role="carrier", flags=OP.PLAT_FLAGS_BIG, off=OP.PLAT_OFF_BIG, size=OP.PLAT_SIZE_BIG, pos=[X, Y, zc])
    crate = dict(role="crate", flags=0x230180, off=(-12, -8, 0), size=(24, 16, 16), pos=[X, Y, zc + 8 * U])
    hero = dict(role="hero", flags=OP.HERO_FLAGS, off=OP.HERO_OFF, size=OP.HERO_SIZE, pos=[X, Y, zc + 8 * U])
    if layout == "chain":
        hero["pos"][2] = zc + 8 * U + 16 * U
    if layout == "crate_on_carrier":
        hero["pos"] = [X + 80 * U, Y + 60 * U, 1]
    ents = [hero]
    if n == 3:
        ents += [crate, plat] if order == "crate_first" else [plat, crate]
    else:
        ents += [plat]
    script = []
    for t in range(rnd.randint(2, 5)):
        w = {}
        for i, e in enumerate(ents):
            if e["role"] in ("carrier", "crate") and rnd.random() < 0.5:
                tx = rnd.choice([0, 0, 65536, -65536, 131072, 98304, -32768])
                ty = rnd.choice([0, 0, 0, 65536, -65536])
                zf = rnd.choice([None, None, 65536, -65536, 0, 32768])
                w[i] = (tx, ty, zf)
            if e["role"] == "hero" and rnd.random() < 0.15:
                w[i] = (rnd.choice([0, 65536, -65536]), 0, None)
        script.append(w)
    return dict(ents=ents, script=script)


def build_real(v):
    p = P()
    for i, e in enumerate(v["ents"]):
        p.add(i, e["flags"], e["off"], e["size"], tuple(e["pos"]), init=False)
    p.s.rebuild_lists()
    for i, e in enumerate(v["ents"]):
        p.s.init_state(i, clamp=False)
    return p


def build_model(v):
    es = []
    for i, e in enumerate(v["ents"]):
        ox, oy, oz = e["off"]
        sx, sy, sz = e["size"]
        es.append(PM.E(idx=i, flags=e["flags"], posx=e["pos"][0], posy=e["pos"][1], posz=e["pos"][2], modx=ox << 16, mody=oy << 16, modz=oz << 16,
                       w=((sx << 16) - 1) if sx else 0, h=((sy << 16) - 1) if sy else 0, d=((sz << 16) - 1) if sz else 0))
    w = PM.World(es)
    w.init_endpass()
    return w


def main():
    nvec = int(sys.argv[1]) if len(sys.argv) > 1 else 300
    seed = int(sys.argv[2]) if len(sys.argv) > 2 else 3
    mut = frozenset(sys.argv[3].split(",")) if len(sys.argv) > 3 else frozenset()
    rnd = random.Random(seed)
    bad = 0
    feats = collections.Counter()
    shown = 0
    for k in range(nvec):
        v = gen(rnd)
        n = len(v["ents"])
        p = build_real(v)
        w = build_model(v)
        w.mut = mut
        for t, wr in enumerate(v["script"]):
            for i, (tx, ty, zf) in wr.items():
                setup_xy(p, i, 85, 24, tx, ty, 1 << 20)
                w.e[i].adjx, w.e[i].adjy = tx, ty
                if zf is not None:
                    p.put(i, "forcez", zf)
                    w.e[i].forcez = zf
            p.tick()
            w.tick(xy=True)
            for i in range(n):
                s = p.snap(i)
                m = w.e[i]
                mm = []
                for key, mv in (("x", m.posx), ("y", m.posy), ("z", m.posz), ("ffx", m.finx), ("ffy", m.finy), ("ffz", m.finalz), ("coll", m.coll), ("ground", m.ground)):
                    if s[key] != mv:
                        mm.append((key, s[key], mv))
                if s["ride"] is not None:
                    feats["ride"] += 1
                if m.finx != m.adjx:
                    feats["copied_xy"] += 1
                if mm:
                    bad += 1
                    if shown < 6:
                        shown += 1
                        print("MISMATCH vector", k, "tick", t, "entity", i, v["ents"][i]["role"], mm[:5])
                    break
    print("xy vectors", nvec, "entity-ticks with a mismatch", bad, dict(feats))


if __name__ == "__main__":
    main()
