import json, collections, sys
sys.stdout.reconfigure(encoding="utf-8")
d = json.load(open("census_z.json", encoding="utf-8"))
ops = [0x1B,0x20,0x21,0x22,0x23,0x3E,0x64,0x65,0x16,0x17,0x63,0x2D,0x25,0x26]
pop = collections.Counter(); popmaps = collections.defaultdict(set)
reach = collections.Counter(); reachmaps = collections.defaultdict(set)
for mid, e in d.items():
    for k, n in e["pop"].items():
        pop[int(k,16)] += n; popmaps[int(k,16)].add(int(mid))
    seen = set()
    for s in e["sites"]:
        if s["kind"] == "primary":
            key = (s["op"], s["pc"])
            if key in seen: continue
            seen.add(key)
            reach[s["op"]] += 1; reachmaps[s["op"]].add(int(mid))
print("opcode  population(sites/maps)   reachable-primary(sites/maps)")
for op in ops:
    print("0x%02X   %5d / %3d          %5d / %3d" % (op, pop[op], len(popmaps[op]), reach[op], len(reachmaps[op])))
for op in (0x21, 0x3E, 0x20, 0x22, 0x23):
    print("maps with population of 0x%02X:" % op, sorted(popmaps[op]))
