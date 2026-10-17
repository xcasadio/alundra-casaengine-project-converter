import json, collections, sys
sys.stdout.reconfigure(encoding="utf-8")
d = json.load(open("census_z.json", encoding="utf-8"))
rows = []
hero_fly = []
unk_fly = collections.Counter()
for mid, e in d.items():
    for s in e["sites"]:
        if s["op"] == 0x1B and s["kind"] == "primary":
            if s["logic"] == "H": hero_fly.append((int(mid), s["pc"], s["root"]))
            if s["logic"] is None: unk_fly[int(mid)] += 1
    for n, t in e["fly_targets"].items():
        sites = [x for x in t["sites"] if x["kind"] == "primary"]
        if not sites: continue
        p = t["prefab"]
        rows.append(dict(map=int(mid), rec=int(n), name=t["name"], flags=p["flags"] if p else None, size=p["size"] if p else None,
                         forces=sorted(set(x["force"] for x in sites)), nsites=len(sites)))
rows.sort(key=lambda r: (r["map"], r["rec"]))
def cls(r):
    f, sz = r["flags"], r["size"]
    if f is None: return "noprefab"
    coll = bool(f & 0x80)
    full = all(v > 0 for v in sz)
    grav = bool(f & 0x100)
    return ("C" if coll else "-") + ("S" if full else "-") + ("G" if grav else "-")
cnt = collections.Counter(cls(r) for r in rows)
print("fly target records (reachable primary 0x1B on a static record):", len(rows), " by class (Collidable,Sized3D,Gravity):", dict(cnt))
print("hero 0x1B sites:", hero_fly)
print("0x1B with unknown logic:", dict(unk_fly))
print()
print("carrier candidates (Collidable + sized 3D), force != 0 at least once:")
cand = [r for r in rows if cls(r)[:2] == "CS" and any(f != 0 for f in r["forces"])]
for r in cand:
    print("map %3d rec %2d %-28s flags 0x%06X size %s forces %s (%d sites)" % (r["map"], r["rec"], (r["name"] or "")[:28], r["flags"], r["size"], r["forces"], r["nsites"]))
print(len(cand), "records on", len(set(r["map"] for r in cand)), "maps")
json.dump(cand, open("carrier_candidates.json", "w"), indent=1)
