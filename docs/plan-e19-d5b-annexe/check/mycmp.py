"""mycmp.py - my own random vectors: the report's pmodel (copied as pmodel_theirs) against the REAL pass 0x80038364 run in my harness (hh.py)."""
import sys, random, collections
from hh import *
import pmodel_theirs as PM

FLAGS = [0x80, 0x180, 0x4180, 0x100, 0x00, 0x1C0, 0x0560C0, 0x0360C0, 0x230180, 0x80 | 0x8000, 0x180 | 0x2000]
BOXES = [((-12, -8, 0), (24, 16, 16)), ((-24, -16, 0), (48, 32, 8)), ((-12, -8, 0), (24, 16, 8)), ((-6, -4, 0), (12, 8, 12)), ((-12, -8, 3), (24, 16, 16)),
         ((-30, -20, 0), (60, 40, 4)), ((-1, -1, 0), (1, 1, 1))]
HBOX = [((-10, -7, 0), (21, 15, 32)), ((-8, -6, 2), (16, 12, 24))]
FORCES = [0, 0, 0, 8192, -8192, 32768, -32768, 65536, -65536, 131072, -131072, 262144, -262144, 1, -1, 12345]


def gen(rnd):
    n = rnd.randint(2, 6)
    G, V = rnd.choice([(128, 4096), (64, 1024), (3, 256), (200, 4096)])
    hb = rnd.choice([0, 0, 1, 3])
    T = hb << 20
    ents = []
    for i in range(n):
        if i == 0:
            flags = rnd.choice([0x03118C, 0x03118C, 0x03118C, 0x03108C, 0x03518C, 0x03118C | 0x40000])
            off, size = rnd.choice(HBOX)
        else:
            flags = rnd.choice(FLAGS)
            off, size = rnd.choice(BOXES)
        ents.append(dict(flags=flags, off=off, size=size, animflags=0x80 if rnd.random() < 0.05 else 0,
                         status=(rnd.choice([2, 2, 2, 3, 4, 1, 0]) if i else 2), blocked=1 if (i and rnd.random() < 0.05) else 0))
    # build a stack tower with random lateral offsets so riders fire
    order = list(range(n))
    rnd.shuffle(order)
    placed = {}
    for k, i in enumerate(order):
        e = ents[i]
        ox, oy, oz = e["off"]
        sx, sy, sz = e["size"]
        if k == 0 or rnd.random() < 0.2:
            px = (100 + rnd.randint(-10, 10)) << 16
            py = (100 + rnd.randint(-6, 6)) << 16
            pz = T + 1 + rnd.choice([0, 0, 65536, 5 * 65536, 30 * 65536, -1])
        else:
            j = rnd.choice(list(placed))
            o = ents[j]
            pp = placed[j]
            ooz = o["off"][2]
            od = ((o["size"][2] << 16) - 1) if o["size"][2] else 0
            if rnd.random() < 0.8:   # rest on j
                pz = pp[2] + (ooz << 16) + od + 1 - (oz << 16) + rnd.choice([0, 0, 0, 0, 1, -1, 65536, 0])
            else:
                pz = pp[2] + (ooz << 16) + od + 1 - (oz << 16) + rnd.choice([-8, 8, 3 * 65536, -3 * 65536]) * 65536 // 65536
            px = pp[0] + rnd.choice([0, 0, 0, 4, -4, 12, 23, -23, 24, 25, 47]) * 65536 + rnd.choice([0, 0, 1])
            py = pp[1] + rnd.choice([0, 0, 0, 3, -3, 9, 15, 16, -15, -16]) * 65536
        placed[i] = (px, py, pz)
        e["pos"] = (px, py, pz)
    nt = rnd.randint(2, 7)
    script = []
    for t in range(nt):
        w = []
        for i in range(n):
            r = rnd.random()
            if r < 0.25:
                w.append((i, "forcez", rnd.choice(FORCES)))
            elif r < 0.30:
                w.append((i, "izf", rnd.choice([0, 0, 1280, -1280, 768, 0x8000, 5])))
        script.append(w)
    return dict(n=n, G=G, V=V, hb=hb, ents=ents, script=script, init=[rnd.choice([0, 0, 0, 32768, -32768, 65536, -65536]) for _ in range(n)])


def build_real(v):
    h = H(gravity=v["G"], visc=v["V"], cell_h=lambda c, r: v["hb"])
    # set all entities first, then lists, then init the end pass (as the author's build; InitializeEntity runs 0x80038064 for every entity)
    for i, e in enumerate(v["ents"]):
        h.add(i, e["flags"], e["off"], e["size"], e["pos"], status=e["status"], animflags=e["animflags"])
        h.p(i, "blocked", e["blocked"])
    h.lists()
    for i in range(v["n"]):
        h.e.call(0x80038064, ent(i))
        h.p(i, "forcez", v["init"][i])
    return h


def build_model(v):
    ents = []
    for i, e in enumerate(v["ents"]):
        ox, oy, oz = e["off"]
        sx, sy, sz = e["size"]
        ents.append(PM.E(idx=i, flags=e["flags"], animflags=e["animflags"], status=e["status"], blocked=e["blocked"], posx=e["pos"][0], posy=e["pos"][1], posz=e["pos"][2],
                         modx=ox << 16, mody=oy << 16, modz=oz << 16, w=((sx << 16) - 1) if sx else 0, h=((sy << 16) - 1) if sy else 0, d=((sz << 16) - 1) if sz else 0,
                         forcez=v["init"][i]))
    w = PM.World(ents, T=v["hb"] << 20, G=v["G"], V=v["V"])
    w.init_endpass()
    return w


def real_snap(h, n):
    out = []
    for i in range(n):
        s = h.snap(i)
        out.append(dict(z=s["z"], fz=s["fz"], ffz=s["ffz"], ride=s["ride"], coll=s["coll"], ground=s["ground"], floor=s["floor"], terr=s["terr"], mz=s["mz"]))
    return out


def model_snap(w):
    out = []
    for x in w.e:
        s = w.snap(x)
        out.append(dict(z=s["z"], fz=s["fz"], ffz=s["ffz"], ride=s["ride"], coll=s["coll"], ground=s["ground"], floor=s["floor"], terr=s["terr"], mz=s["mz"]))
    return out


def main(nvec, seed):
    rnd = random.Random(seed)
    bad = 0
    ticks = 0
    carrier_ticks = 0
    stats = collections.Counter()
    for k in range(nvec):
        v = gen(rnd)
        h = build_real(v)
        w = build_model(v)
        a, b = real_snap(h, v["n"]), model_snap(w)
        if a != b:
            bad += 1
            print("INIT MISMATCH vec", k); print(a); print(b)
            if bad > 5: break
            continue
        for t, writes in enumerate(v["script"]):
            for (i, name, val) in writes:
                h.p(i, name, val)
                setattr(w.e[i], {"forcez": "forcez", "izf": "izf"}[name], val)
            h.tick(); w.tick()
            ticks += 1
            a, b = real_snap(h, v["n"]), model_snap(w)
            for x in w.e:
                if x.ride is not None: stats["ride"] += 1
                if x.coll: stats["coll"] += 1
            if a != b:
                bad += 1
                print("MISMATCH vec %d tick %d" % (k, t))
                for i in range(v["n"]):
                    if a[i] != b[i]:
                        print("  ent", i, "flags %x" % v["ents"][i]["flags"], "real", a[i], "model", b[i])
                break
        if bad > 5:
            break
    print("vectors", nvec, "ticks", ticks, "mismatching vectors", bad, dict(stats))

if __name__ == "__main__":
    main(int(sys.argv[1]), int(sys.argv[2]))
