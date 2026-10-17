import json, collections, sys
sys.stdout.reconfigure(encoding="utf-8")
d = json.load(open("census_z.json", encoding="utf-8"))
CLASSES = [
 ("Plateformes flottantes (grande/petite/fragile) + asiba", ("Plateforme flottante", "asiba")),
 ("Bulles porteuses (Bulle .0-1-2 support)", ("Bulle",)),
 ("Trappes d'escalier (grande/petite)", ("Trappe d",)),
 ("Blocs transparents (1x1x2, 8x2x1, 6x10)", ("Bloc transparent", "Gros bloc transparent")),
 ("Bois flottant sur l'eau", ("Bois flottant",)),
 ("Interrupteurs a pieteiner (descendent)", ("Interrupteur (pi",)),
 ("Interrupteur a ballon", ("Interrupteur \u00e0 ballon",)),
 ("Grilles de fer (grande/petite)", ("Grille de fer",)),
 ("Portes / statue / bloc a pousser", ("Porte (grande)", "Statue d", "Bloc \u00e0 pousser")),
]
rows = []
for mid, e in d.items():
    for n, t in e["fly_targets"].items():
        sites = [x for x in t["sites"] if x["kind"] == "primary"]
        if not sites or not t["prefab"]:
            continue
        p = t["prefab"]
        if not (p["flags"] & 0x80) or not all(v > 0 for v in p["size"]):
            continue
        forces = sorted(set(x["force"] for x in sites))
        rows.append(dict(map=int(mid), rec=int(n), name=t["name"] or "", flags=p["flags"], size=p["size"], forces=forces, nsites=len(sites), nz=any(f != 0 for f in forces)))
def cls_of(r):
    for label, keys in CLASSES:
        if any(r["name"].startswith(k) for k in keys):
            return label
    return None
agg = collections.OrderedDict()
for label, _ in CLASSES:
    agg[label] = dict(recs=0, maps=set(), forces=set(), grav=0)
other = collections.Counter()
for r in rows:
    c = cls_of(r)
    if c is None:
        other[r["name"]] += 1
        continue
    if not r["nz"]:
        continue
    a = agg[c]
    a["recs"] += 1; a["maps"].add(r["map"]); a["forces"].update(f for f in r["forces"] if f); a["grav"] += 1 if r["flags"] & 0x100 else 0
tot_recs = tot_maps = 0
allmaps = set()
for label, a in agg.items():
    print("%-58s recs %3d maps %3d gravity-bit %2d  forces(px/tick=f/256) %s" % (label, a["recs"], len(a["maps"]), a["grav"], sorted(a["forces"])[:3] + ["..."] + sorted(a["forces"])[-3:] if len(a["forces"]) > 6 else sorted(a["forces"])))
    tot_recs += a["recs"]; allmaps |= a["maps"]
print("TOTAL ride-relevant carrier records with a non-zero 0x1B:", tot_recs, "on", len(allmaps), "maps")
print("maps:", sorted(allmaps))
print("other collidable sized 0x1B targets (NPCs, items...):", sum(other.values()), dict(other.most_common(12)))
json.dump(dict(carrier_maps=sorted(allmaps), by_class={k: dict(recs=v["recs"], maps=sorted(v["maps"])) for k, v in agg.items()}), open("carrier_census.json", "w"), indent=1)
# the 0x21 / 0x22 / 0x23 site context
print()
for op in (0x21, 0x22, 0x23):
    print("sites of 0x%02X (reachable primary):" % op)
    seen = set()
    for mid, e in d.items():
        for s in e["sites"]:
            if s["op"] == op and s["kind"] == "primary" and (mid, s["pc"]) not in seen:
                seen.add((mid, s["pc"]))
                print("   map %s pc %d logic %s arg %s root %s" % (mid, s["pc"], s["logic"], s["arg"], s["root"]))
