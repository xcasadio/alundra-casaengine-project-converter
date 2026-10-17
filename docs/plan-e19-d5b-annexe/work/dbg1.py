import sys, random, json
sys.path.insert(0, ".")
import vec_p as V
rnd = random.Random(1)
want = [int(a) for a in sys.argv[1:]]
for k in range(max(want)+1):
    v = V.gen_vector(rnd)
    if k in want:
        rec = {}
        mm = V.run_vector(v, record=rec)
        print("=== vector", k, "T_hb", v["T_hb"], "G,V", v["G"], v["V"])
        for i, e in enumerate(v["ents"]):
            print(" ent", i, "flags %06X af %X st %d bl %d off %s size %s pos %s init_force %d" % (e["flags"], e["animflags"], e["status"], e["blocked"], e["off"], e["size"], e["pos"], v["init_force"][i]))
        print(" script", v["script"])
        print(" mismatches", mm[:8])
        for t, snap in enumerate(rec["real"]):
            print("  real t%d" % t, [(s["z"], s["fz"], s["ffz"], s["ride"], s["coll"], s["ground"], s["floor"]) for s in snap])
