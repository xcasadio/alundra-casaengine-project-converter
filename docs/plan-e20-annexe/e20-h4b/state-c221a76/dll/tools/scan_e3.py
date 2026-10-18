import json, glob, os, sys, collections
root = sys.argv[1]
pairs = collections.defaultdict(list)
maps_with = set()
codes = {3: [], 13: [], 28: []}
total_maps = 0
for p in sorted(glob.glob(os.path.join(root, "map_*.json"))):
    name = os.path.basename(p)
    if "_" in name[4:-5]:
        continue
    try:
        d = json.load(open(p, encoding="utf-8"))
    except Exception:
        continue
    si = d.get("SpriteInfo")
    if not si:
        continue
    total_maps += 1
    for idx, rec in enumerate(si.get("SpriteRecords") or []):
        if not rec:
            continue
        h = rec.get("Header")
        if not h:
            continue
        e = h.get("ProgramDeactivate")
        if e in codes:
            codes[e].append((name, idx))
for e, lst in codes.items():
    idxs = collections.Counter(i for _, i in lst)
    print("E%d: pairs=%d maps=%d distinct_idx=%d" % (e, len(lst), len({n for n, _ in lst}), len(idxs)))
    print("   idx:count", sorted(idxs.items()))
print("maps scanned", total_maps)
