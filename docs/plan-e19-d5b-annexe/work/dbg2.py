import sys, random, json
sys.path.insert(0, ".")
import vec_p as V
rnd = random.Random(7)
bads = []
for k in range(4000):
    v = V.gen_vector(rnd)
    rec = {}
    mm = V.run_vector(v, record=rec)
    if mm:
        bads.append((k, v, rec, mm))
print("bad", len(bads))
for k, v, rec, mm in bads[:12]:
    print("=== vector", k, "mm", mm[:5])
    if "-v" in sys.argv:
        for i, e in enumerate(v["ents"]):
            print("  ent", i, "flags %06X af %X st %d bl %d off %s size %s pos %s init_force %d" % (e["flags"], e["animflags"], e["status"], e["blocked"], e["off"], e["size"], e["pos"], v["init_force"][i]))
        print("  script", v["script"])
