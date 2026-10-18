"""sprite21_dump.py (h4d1 discovery, read-only): the global sprite 21 (fire 2 column of light) as the extraction and the project export hold it:
header flags, program codes, animation 0 frame by frame (delay, collision box), balance sheet attacks; plus the export record (BankKey alundra_21)."""
import json
import sys

ROOT = "D:/development/repo/alundra-casaengine-project-converter/"
d = json.load(open(ROOT + "data-extracted/data/map_alundra.json", encoding="utf-8-sig"))
recs = {x["Header"]["Sector5Id"]: x for x in d["SpriteInfo"]["SpriteRecords"] if x}
r = recs[21]
h = r["Header"]
print("HEADER", {k: h[k] for k in ("Sector5Id", "MoreFlags", "CanPickup", "FlagsPortraitShadowType", "ProgramLoad", "ProgramTick", "ProgramTouch", "ProgramDeactivate", "ProgramInteract",
                                    "OffsetX", "OffsetY", "OffsetZ", "SizeX", "SizeY", "SizeZ", "BreakEffect", "Contents")})
print("AnimSets", len(r["AnimSets"]))
for si, s in enumerate(r["AnimSets"]):
    print(" set", si, {k: s[k] for k in ("IsZForceApplied", "Speed", "Unknown", "Flags", "Sfx", "Acceleration")}, "anims", len(s["PreloadedAnims"]))
    for ai, a in enumerate(s["PreloadedAnims"]):
        print("  anim", ai, "frames", a["NumberOfFrames"])
        tot = 0
        rows = []
        for fi, f in enumerate(a["Frames"]):
            dl = f["Delay"]
            c = f["CollisionData"]
            tot += dl & 0x7F
            box = (c["OffsetX"], c["OffsetY"], c["OffsetZ"], c["Width"], c["Depth"], c["Height"]) if c else (None,) * 6
            rows.append((fi, dl, dl & 0x7F, bool(dl & 0x80), box))
        print("   total ticks (delay&0x7F):", tot)
        last = None
        for row in rows:
            key = (row[2], row[3], row[4])
            if key != last:
                print("   frame %2d delay %#04x (%d ticks, hi=%s) box off,size %s" % (row[0], row[1], row[2], row[3], row[4]))
                last = key
        if si == 0 and ai == 0:
            print("   (all frames:)", [(x[0], x[2], x[4][3:]) for x in rows])
b = json.load(open(ROOT + "data-extracted/data/BALANCE.BIN.json", encoding="utf-8-sig"))
e = b["EntriesBySpriteIndex"][21]
print("BALANCE sprite 21:", json.dumps(e)[:1500])
bj = json.load(open(ROOT + "alundra-project/Data/balance.json", encoding="utf-8-sig"))
print("export balance.json keys", list(bj.keys())[:8] if isinstance(bj, dict) else len(bj))
sr = json.load(open(ROOT + "alundra-project/Data/sprite-records.json"))
for k, v in sr.items():
    if v.get("BankKey") == "alundra_21":
        print("EXPORT record", k, {kk: v[kk] for kk in v if kk not in ("IdsvAnimDirs",)})
        for ad in v["IdsvAnimDirs"]:
            if ad["Direction"] == 0:
                print("  anim", ad["Anim"], "dir", ad["Direction"], "frames", len(ad["Frames"]), "end", ad["End"], ad.get("ChainTo"))
