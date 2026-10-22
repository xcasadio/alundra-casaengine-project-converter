"""agg.py - aggregates census3.json: exposure of the Z carriers to passengers other than the hero."""
import json, collections, sys
sys.stdout.reconfigure(encoding="utf-8")
r = json.load(open("census3.json", encoding="utf-8"))
zmaps = {mid: e for mid, e in r.items() if e["zcarriers"]}
print("maps with a Z carrier (a collidable sized 0x1B target):", len(zmaps), "carrier records:", sum(len(e["zcarriers"]) for e in zmaps.values()))
with_pas = {mid: e for mid, e in zmaps.items() if sum(e["passengers_capable"].values())}
print("of which with at least one passenger-capable record spawned at load (collidable + gravity + not NoRiders):", len(with_pas))
tot = collections.Counter()
for e in zmaps.values():
    tot.update(e["passengers_capable"])
print("passenger-capable records on those maps, by type (top 15):")
for k, v in tot.most_common(15):
    print("  %-40s %d" % (k, v))
print("total:", sum(tot.values()))
mov_prefix = ("Caisse", "Cruche", "Tonneau", "Pierre g", "Pierre tr", "Tronc", "Herbe transportable", "Objet ", "Bloc de glace", "Pilier poussable", "Torche", "Rocher tortue")
mov = {mid for mid, e in zmaps.items() if any(k.startswith(mov_prefix) for k in e["passengers_capable"])}
print("Z-carrier maps that also hold liftable/pushable objects at load:", len(mov))
chest = {mid for mid, e in zmaps.items() if any(k.startswith(("Coffre",)) for k in e["passengers_capable"])}
print("Z-carrier maps that also hold chests:", len(chest))
npc = {mid for mid, e in zmaps.items() if any(not k.startswith(mov_prefix + ("Coffre", "I", "Statue", "Ronces", "Colonne", "YM08", "Armure", "Mini", "Chaîne", "Boîte")) for k in e["passengers_capable"])}
print("Z-carrier maps with another passenger-capable type (NPCs and others):", len(npc))
