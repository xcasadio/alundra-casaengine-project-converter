"""survey_h2.py - prefab flags / box / AnimSets / animation ends of the records used by the h2 replays (reads the exported project, writes nothing)."""
import sys, os, json
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
sys.stdout.reconfigure(encoding="utf-8")
from m import Map, DATA
SPR = json.load(open(DATA + "/sprite-records.json", encoding="utf-8"))

def show(mid, idxs=None):
    mp = Map(mid)
    print("MAP", mid, mp.W, "x", mp.H, "G/V", mp.tm.get("custom_properties", {}).get("Gravity"), mp.tm.get("custom_properties", {}).get("ZViscosity"))
    for r in mp.records:
        if idxs is not None and int(r["Index"]) not in idxs:
            continue
        h = SPR.get(r["PrefabAssetId"])
        fl = None
        if h:
            fl = h["MoreFlags"] | (h["CanPickup"] << 8) | (h["FlagsPortraitShadowType"] << 16)
        print(" rec", r["Index"], "name", r.get("EntityName"), "prefab", r["PrefabAssetId"], "X", r["XPos"], "Y", r["YPos"], "Height", r["Height"],
              "dir", r.get("SpriteDirection"), "C", r.get("EventCodesC_TickIndex"), "B?", r.get("EventCodesA_LoadIndex"))
        if h:
            print("    flags %#x (Gravity bit 0x100: %s) off (%s,%s,%s) size (%s,%s,%s)" % (fl, bool(fl & 0x100), h["OffsetX"], h["OffsetY"], h["OffsetZ"], h["SizeX"], h["SizeY"], h["SizeZ"]))
            print("    AnimSets:", [(a["Anim"], a["Speed"], a["Acceleration"], a["IsZForceApplied"]) for a in h["AnimSets"] if a["IsZForceApplied"] or a["Anim"] < 6])
            ends = {}
            for e in h["IdsvAnimDirs"]:
                ends.setdefault(e["Anim"], set()).add((e["End"], e.get("ChainTo")))
            print("    anim ends:", {k: sorted(v, key=str) for k, v in ends.items() if k < 8})

for mid, idxs in [(10, {69, 81, 98}), (165, None), (172, None), (179, {8}), (478, {3, 4}), (362, None), (174, None), (181, None)]:
    try:
        show(mid, idxs)
    except Exception as e:
        print("ERR", mid, e)
