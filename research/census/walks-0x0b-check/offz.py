"""offz.py - OffsetZ / flags of the actors of the walk states (census0b rows), checker side."""
import collections
import json
import os
import pickle
import sys

sys.stdout.reconfigure(encoding="utf-8")
ROOT = "D:/development/repo/alundra-casaengine-project-converter"
HERE = os.path.dirname(os.path.abspath(__file__))
C0 = os.path.join(os.path.dirname(HERE), "census0b")
sys.path.insert(0, HERE)
SPR = json.load(open(ROOT + "/alundra-project/Data/sprite-records.json", encoding="utf-8-sig"))
HERO = "192c2eeb-bdda-5bb4-92d6-39a4ae298307"
from pop import maps  # noqa: E402
import glob  # noqa: E402

M = maps()
_recs = {}


def recs(mid):
    if mid not in _recs:
        folder = M[mid][0]
        tm = json.load(open(glob.glob(folder + "/tilemap/*.tileMap")[0], encoding="utf-8"))
        layers = {l["name"]: l["objects"] for l in tm.get("object_layers", [])}
        _recs[mid] = {int(o["custom_properties"]["Index"]): o["custom_properties"] for o in layers.get("Entities", [])}
    return _recs[mid]


def pid_of(mid, key):
    if key == "H":
        return HERO
    return recs(mid)[key[1]].get("PrefabAssetId")


rows = pickle.load(open(C0 + "/rows_DLL.pkl", "rb"))
oz = collections.Counter()
ozsites = collections.defaultdict(set)
for r in rows:
    for cls, det in r.get("all") or []:
        key = det.get("key")
        if key is None:
            continue
        pid = pid_of(r["map"], key)
        h = SPR.get(pid)
        if h is None:
            oz["nohdr"] += 1
            continue
        oz[h.get("OffsetZ")] += 1
        ozsites[h.get("OffsetZ")].add((r["map"], r["pc"]))
print("OffsetZ over walk states:", oz.most_common())
print({k: len(v) for k, v in ozsites.items()})
print("sites with OffsetZ != 0:", sorted(s for k, v in ozsites.items() if k not in (0, None) for s in v)[:60])
