"""compare.py - re-derive every walk state of the census runs with mysim.py and compare per state.

Usage: python compare.py DLL | ORIG0 | ORIGOBL  (ORIGOBL = ORIG0 states re-run WITHOUT the oblique slide, i.e. the census rule)
The start (pin), direction, radius, animation and actor of each state are taken from the census rows (the reach / start model
is not re-derived here); the verdict of the walk is re-derived.
"""
import collections
import glob
import json
import os
import pickle
import sys

sys.stdout.reconfigure(encoding="utf-8")
HERE = os.path.dirname(os.path.abspath(__file__))
C0 = os.path.join(os.path.dirname(HERE), "census0b")
sys.path.insert(0, HERE)
import mysim as S  # noqa: E402
from pop import maps  # noqa: E402

M = maps()
_md = {}


def mapdata(mid):
    if mid not in _md:
        folder = M[mid][0]
        tm = json.load(open(glob.glob(folder + "/tilemap/*.tileMap")[0], encoding="utf-8"))
        layers = {l["name"]: l["objects"] for l in tm.get("object_layers", [])}
        recs = {int(o["custom_properties"]["Index"]): o["custom_properties"] for o in layers.get("Entities", [])}
        cp = tm.get("custom_properties", {})
        cells = json.loads(cp["AlundraCells"]) if "AlundraCells" in cp else None
        F = S.Field(cells, tm["map_size"]["w"], tm["map_size"]["h"]) if cells else None
        _md[mid] = (F, recs)
    return _md[mid]


def pid_of(mid, key):
    if key == "H":
        return S.HERO
    return mapdata(mid)[1][key[1]].get("PrefabAssetId")


def speed(pid, anim):
    h = S.SPR.get(pid)
    if h is None or anim is None:
        return None
    for a in h["AnimSets"]:
        if a["Anim"] == anim:
            return a["Speed"]
    return 0


def verdict(rule, mid, det):
    F, recs = mapdata(mid)
    key = det.get("key")
    pid = pid_of(mid, key)
    h = S.SPR.get(pid)
    if F is None or h is None or det.get("pin") is None or det.get("dir") is None:
        return "n/a", None
    sp = speed(pid, det.get("anim"))
    if not sp:
        return "zero", None
    b = S.Body(h)
    X0 = int(round(det["pin"][0] * S.U))
    Y0 = int(round(det["pin"][1] * S.U))
    if rule == "DLL":
        k, X, Y, why = S.walk_dll(F, b, X0, Y0, det["dir"], det["rad"])
    else:
        k, X, Y, why = S.walk_orig(F, b, X0, Y0, det["dir"], det["rad"], oblique_slide=(rule != "ORIGOBL"))
    return {"pass": "pass", "blocked": "blocked", "left": "indet", "zero": "zero"}[k], (X / S.U, Y / S.U, why)


def main():
    rule = sys.argv[1]
    src = {"DLL": "DLL", "ORIG0": "ORIG0", "ORIGOBL": "ORIG0"}[rule]
    rows = pickle.load(open(C0 + "/rows_%s.pkl" % src, "rb"))
    agree = collections.Counter()
    dis = []
    for r in rows:
        if not r.get("reached"):
            continue
        for cls, det in r.get("all") or []:
            mine, info = verdict(rule, r["map"], det)
            if mine == "n/a":
                agree["n/a"] += 1
                continue
            if mine == cls:
                agree["agree " + cls] += 1
            else:
                agree["DIFF census %s mine %s" % (cls, mine)] += 1
                dis.append((r["map"], r["pc"], cls, mine, det.get("actor"), det.get("dir"), det.get("pin"), det.get("rad"),
                            det.get("n"), det.get("end"), info))
    for k, v in sorted(agree.items()):
        print("%-40s %d" % (k, v))
    pickle.dump(dis, open(HERE + "/dis_%s.pkl" % rule, "wb"))
    sites = collections.defaultdict(list)
    for d in dis:
        sites[(d[0], d[1])].append(d)
    print("sites with a disagreeing state:", len(sites))
    for k, ds in sorted(sites.items())[:400]:
        d = ds[0]
        print("  %4d @%-5d n=%d census %s mine %s | %s dir %s pin %s r %s census_n %s end %s mine %s" % (
            k[0], k[1], len(ds), d[2], d[3], d[4], d[5], d[6], d[7], d[8], d[9], d[10]))


if __name__ == "__main__":
    main()
