"""Extract the parts of data-extracted/data/map_N.json that the roster needs into one compact pickle
(cache/maps.pkl, scratch folder only). Read-only on the repository. Run: python -B extract_maps.py"""
import glob
import json
import os
import pickle
import re
import sys
import time

DATA = r"D:\development\repo\alundra-casaengine-project-converter\data-extracted\data"
OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "cache")
os.makedirs(OUT, exist_ok=True)

ENT_KEYS = ("IsEnabled", "SpriteDirection", "SpriteTableIndex", "XPos", "YPos", "Height", "XMin", "YMin", "XMax", "YMax",
            "EventCodesA_LoadIndex", "EventCodesB_MapIndex", "EventCodesC_TickIndex", "EventCodesD_TouchIndex",
            "EventCodesE_DeactivateIndex", "EventCodesF_InteractIndex", "_10", "Contents")


def extract(path):
    with open(path, encoding="utf-8") as fh:
        d = json.load(fh)
    inf = d["Info"]
    si = d["SpriteInfo"]
    ents = []
    for i, e in enumerate(si["Entities"]["Entities"]):
        if e is None:
            continue
        r = {k: e.get(k) for k in ENT_KEYS}
        r["idx"] = i
        ents.append(r)
    ec = si["EventCodes"]
    recs = {}
    for i, r in enumerate(si["SpriteRecords"]):
        if r:
            h = r["Header"]
            recs[i] = {k: h.get(k) for k in ("Sector5Id", "MoreFlags", "CanPickup", "FlagsPortraitShadowType", "ProgramLoad", "ProgramTick",
                                              "ProgramTouch", "ProgramDeactivate", "ProgramInteract", "BreakEffect", "Contents",
                                              "OffsetX", "OffsetY", "OffsetZ", "SizeX", "SizeY", "SizeZ")}
    portals = [{"dest": p["DestMapId"], "x1": p["X1"], "y1": p["Y1"]} for p in (inf.get("Portals") or []) if p]
    return {
        "mapId": inf["MapId"],
        "info": {k: inf.get(k) for k in ("Gravity", "ZViscosity", "BalanceLevel", "C", "D", "E", "F", "_10", "_11")},
        "entities": ents,
        "tables": {s: ec["EventCodes%sTable" % s] for s in "ABCDEF"},
        "codes": ec["Codes"],
        "spriteTable": si["SpriteTable"],
        "spriteRecords": recs,
        "mapEvents": [m for m in (si["MapEvents"]["Records"] or []) if m],
        "portals": portals,
        "mapCoords": (d["Map"]["Width"], d["Map"]["Height"]),
    }


def main():
    files = sorted(glob.glob(os.path.join(DATA, "map_[0-9]*.json")), key=lambda p: int(re.search(r"map_(\d+)\.json", p).group(1)))
    maps = {}
    t0 = time.time()
    for n, p in enumerate(files):
        fid = int(re.search(r"map_(\d+)\.json", p).group(1))
        maps[fid] = extract(p)
        if n % 40 == 0:
            print(n, "/", len(files), f"{time.time() - t0:.0f}s", flush=True)
    # global bank
    with open(os.path.join(DATA, "map_alundra.json"), encoding="utf-8") as fh:
        g = json.load(fh)
    grecs = {}
    for i, r in enumerate(g["SpriteInfo"]["SpriteRecords"]):
        if r:
            h = r["Header"]
            grecs[i] = {k: h.get(k) for k in ("Sector5Id", "MoreFlags", "CanPickup", "FlagsPortraitShadowType", "ProgramLoad", "ProgramTick",
                                               "ProgramTouch", "ProgramDeactivate", "ProgramInteract", "BreakEffect", "Contents",
                                               "OffsetX", "OffsetY", "OffsetZ", "SizeX", "SizeY", "SizeZ")}
    with open(os.path.join(OUT, "maps.pkl"), "wb") as fh:
        gec = g["SpriteInfo"]["EventCodes"]
        pickle.dump({"maps": maps, "alundra": grecs,
                     "alundraEvents": {"tables": {s_: gec["EventCodes%sTable" % s_] for s_ in "ABCDEF"}, "codes": gec["Codes"]}}, fh)
    print("done", len(maps), "maps,", len(grecs), "global records,", f"{time.time() - t0:.0f}s")


if __name__ == "__main__":
    sys.stdout.reconfigure(encoding="utf-8")
    main()
