"""m331.py - is map 331 a copy of map 10? (code, tables, records, map events, cells)"""
import glob
import json
import sys

sys.stdout.reconfigure(encoding="utf-8")
sys.path.insert(0, ".")
from pop import maps  # noqa: E402

M = maps()


def load(mid):
    folder, evf = M[mid]
    ev = json.load(open(evf, encoding="utf-8"))
    tm = json.load(open(glob.glob(folder + "/tilemap/*.tileMap")[0], encoding="utf-8"))
    layers = {l["name"]: [o["custom_properties"] for o in l["objects"]] for l in tm.get("object_layers", [])}
    cells = json.loads(tm["custom_properties"]["AlundraCells"])
    return ev, layers, cells, tm["map_size"]


a, b = load(10), load(331)
print("codes equal", a[0]["Codes"] == b[0]["Codes"], len(a[0]["Codes"]))
print("tables equal", all(a[0]["EventCodes%sTable" % k] == b[0]["EventCodes%sTable" % k] for k in "ABCDEF"))
for name in sorted(set(a[1]) | set(b[1])):
    la, lb = a[1].get(name, []), b[1].get(name, [])
    diff = [i for i, (x, y) in enumerate(zip(la, lb)) if x != y]
    keys = set()
    for i in diff:
        keys |= {k for k in set(la[i]) | set(lb[i]) if la[i].get(k) != lb[i].get(k)}
    print("layer %-10s %d vs %d, differing objects %d, keys %s" % (name, len(la), len(lb), len(diff), sorted(keys)[:8]))
W = a[3]["w"]
for col in ("walkability", "ground_property", "slope", "height"):
    d = [(i % W, i // W) for i, (x, y) in enumerate(zip(a[2][col], b[2][col])) if x != y]
    print("cells %-16s %d differ: %s" % (col, len(d), d[:30]))
