"""Proposed slices: the type groups are chosen by hand (families sharing handler codes, story presence), the numbers are computed.
Reads cache/report_data.pkl and cache/census.pkl. Writes slices.tsv and cache/slices.json. Run: python -B slices.py"""
import json
import os
import pickle
import sys

import common

sys.stdout.reconfigure(encoding="utf-8")
HERE = common.HERE
d = pickle.load(open(os.path.join(HERE, "cache", "report_data.pkl"), "rb"))
c = pickle.load(open(os.path.join(HERE, "cache", "census.pkl"), "rb"))
names = common.load_entity_names()
cat = common.load_maps_catalog()
H = {h["key"]: h for h in d["handlers"]}
R = {r["type"]: r for r in d["roster"]}
T = c["types"]

# helpers missing in the DLL (checked by grep of the addresses in Alundra/Scripts/*.cs) and the callee names that stand for them in handlers.tsv
MISSING_HELPERS = {"CanMoveForward": "0x80080260", "SetEntityRandomDirection": "0x80080030", "GetEntityTileHeight": "0x8003AB54",
                   "0x800808dc": "0x800808DC (unnamed in the decompilation; calls GetEntityTileHeight)", "TryAttackPlayer": "0x80080704",
                   "CreateEffectEntity": "0x8003BDD8 (only the break effect is ported)"}

SLICES = [
    ("S0", "Socle partagé : inscription cumulative des types, alias de E, formes HitReaction et setters A, E3 et C36, aides manquantes", [], "E14.a0, a0b, a1 faits"),
    ("S1", "Route du jour 2 (cartes 14 et 15) : Poisson, Guêpe, Orc (masse, hache, armure de fer)", [392, 455, 341, 338, 344], "S0"),
    ("S2", "Slimes géants de la 44 (jour 3)", [459, 460], "S0"),
    ("S3", "Slime gélatineux (rouvre A1 et D1 au type 329 ; projectile 330)", [329], "S0 ; projectile 330 (C24 vide, E3)"),
    ("S4", "Mimming et mimique", [377, 386, 404], "S0"),
    ("S5", "Roches, insectes, ver", [437, 441, 395, 401, 410], "S0 ; projectile 411 ; C7 et C8 créent des entités"),
    ("S6", "Momies, fantôme, zombie, loup-garou", [425, 454, 428, 449, 482], "S0"),
    ("S7", "Muruta à l'arc et au boomerang", [350, 356], "S0 ; projectiles 351 (C24 vide, E3) et 357 (C36, E3)"),
    ("S8", "Hommes-lézards (épée, lance, projectiles)", [362, 365, 371], "S0 ; projectiles 366 et 372 (E3)"),
    ("S9", "Casters et divers : Roper, Homme de boue, Homme-ombre, Champignon, Fourneau, Oiseau de feu, Homme de bois, P-Zoldia", [389, 456, 431, 440, 443, 467, 473, 461, 452, 453], "S0 ; projectiles 432, 444, 468, 474"),
    ("S10", "Slime géant d'Elane (œil, corps) et petit slime", [471, 472, 478], "S0 ; C1 du Beannoïde pour 478 (inscription cumulative)"),
]
# children with no literal parent in the decompilation, attached to a slice by NAME only (INFERENCE)
NAME_LINKED = {488: [489, 490, 491, 492, 504], 495: [496, 502, 503]}
BOSS_TYPES = [t for t, r in R.items() if r["role"] == "boss" and r["status"].startswith("none")] + [495]


def children_of(types):
    """Types created by the handlers of `types` (decompilation literals), followed transitively; the types themselves excluded."""
    seen = set()
    todo = list(types)
    while todo:
        t = todo.pop()
        hdr = R[t]["hdr"]
        for slot, code in ((0, hdr[0]), (2, hdr[1]), (4, hdr[3])):
            if code == 0 and slot != 0:
                continue
            h = H.get(f"{'ACDEF'[ {0: 0, 2: 1, 3: 2, 4: 3, 5: 4}[slot] ]}{code}")
            if not h:
                continue
            fn = h["func"].split(".")[-1]
            for ch in d["spawn_types_by_func"].get(fn, []):
                if ch in R and ch not in seen and ch not in types and ch != t:
                    seen.add(ch)
                    todo.append(ch)
    return sorted(seen)


def slice_stats(types):
    keys = set()
    recs = 0
    maps = set()
    for t in types:
        r = R[t]
        recs += r["recs"]
        maps |= set(r["mapIds"])
        keys |= set(r["missing"])
    words = sum(H[k]["words"] for k in keys if k in H and k != "A0")
    rng = sum(H[k]["rng"] for k in keys if k in H)
    spawn = sum(H[k]["spawnsN"] for k in keys if k in H)
    helpers = {}
    for k in sorted(keys):
        for n in H[k]["calls"]:
            key = n.split(" (")[0]
            if key in MISSING_HELPERS:
                helpers.setdefault(key, set()).add(k)
    storymaps = sorted({m for m in maps if any(m in lst for _, lst, _ in d["STORY"])})
    kids = [k for k in children_of(types) if R[k]["role"] != "object/prop"]
    named = sorted({k for t in types for k in NAME_LINKED.get(t, [])})
    return {"nameLinked": named, "children": kids, "types": list(types), "records": recs, "maps": len(maps), "perKiloWord": round(1000.0 * len(maps) / words, 1) if words else None, "keys": sorted(keys), "words": words, "rng": rng, "spawnCalls": spawn,
            "missingHelpers": {k: sorted(v) for k, v in sorted(helpers.items())}, "storyMaps": storymaps}


rows = []
out = []
for sid, label, types, deps in SLICES:
    st = slice_stats(types)
    st.update({"id": sid, "label": label, "deps": deps})
    out.append(st)
bstats = []
for t in sorted(set(BOSS_TYPES)):
    st = slice_stats([t])
    bstats.append((t, st))
# order: most maps first, then shortest (maps desc, EXE words asc): the criterion is data, not the story (unknown beyond day 4)
bstats.sort(key=lambda ts: (-ts[1]["maps"], ts[1]["words"], ts[0]))
for i, (t, st) in enumerate(bstats):
    st.update({"id": f"B{i + 1}", "label": "Boss : " + names[t][0].lstrip("◆"), "deps": "S0 (E11 : voir 5.1 si la clé est listée)"})
    out.append(st)

with open(os.path.join(HERE, "slices.tsv"), "w", encoding="utf-8", newline="") as fh:
    fh.write("\t".join(["id", "label", "types", "records", "maps", "story_chain_maps", "new_handler_keys", "exe_words_of_new_keys(upper bound)", "maps_per_1000_words", "rng_sites", "spawn_calls", "missing_helpers", "children_created(decompilation)", "children_linked_by_name(INFERENCE)", "depends_on"]) + "\n")
    for s in out:
        fh.write("\t".join([s["id"], s["label"], ",".join(f"{t}:{names[t][0].lstrip(chr(0x25c6))}" for t in s["types"]), str(s["records"]), str(s["maps"]),
                            ",".join(map(str, s["storyMaps"])), ",".join(s["keys"]), str(s["words"]), str(s["perKiloWord"]), str(s["rng"]), str(s["spawnCalls"]),
                            ";".join(f"{k}->{','.join(v)}" for k, v in s["missingHelpers"].items()), s["deps"]]) + "\n")
with open(os.path.join(HERE, "cache", "slices.json"), "w", encoding="utf-8") as fh:
    json.dump(out, fh, ensure_ascii=False, indent=1)
for s in out:
    print(s["id"], s["label"][:48], "| types", len(s["types"]), "recs", s["records"], "maps", s["maps"], "story", s["storyMaps"], "words", s["words"], "rng", s["rng"], "spawn", s["spawnCalls"], "helpers", list(s["missingHelpers"]))
