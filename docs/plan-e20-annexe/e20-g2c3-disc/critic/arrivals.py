"""arrivals.py - read-only: for each drop map, the portals of the exported project that arrive in it (DestMapId, DestTileX, DestTileY), and whether the arrival tile is in the spawn box of the drops / torches / the zone of the map event B[1] of the map 92."""
import json, glob, os
ROOT = "D:/development/repo/alundra-casaengine-project-converter/alundra-project/Maps"
DATA = "D:/development/repo/alundra-casaengine-project-converter/data-extracted/data"
portals = []
for f in glob.glob(ROOT + "/*/*/tilemap/*.tileMap"):
    try:
        d = json.load(open(f, encoding="utf-8-sig"))
    except Exception:
        continue
    for L in d.get("object_layers", []):
        if "ortal" in (L.get("name") or ""):
            for o in L.get("objects", []):
                cp = o.get("custom_properties", {})
                portals.append((os.path.basename(f), cp.get("DestMapId"), cp.get("DestTileX"), cp.get("DestTileY")))
for target in (87, 88, 90, 92, 94, 158):
    arr = [(n, int(x), int(y)) for n, m, x, y in portals if m == str(target)]
    d = json.load(open(DATA + "/map_%d.json" % target, encoding="utf-8-sig"))
    ents = [e for e in d["SpriteInfo"]["Entities"]["Entities"] if e]
    recs = d["SpriteInfo"]["SpriteRecords"]
    print("map", target, "arrivals", arr)
    for n, x, y in arr:
        born_drops = [i for i, e in enumerate(d["SpriteInfo"]["Entities"]["Entities"])
                      if e and e["IsEnabled"] and (e["SpriteDirection"] & 0x40) and e["SpriteTableIndex"] == 135
                      and e["XMin"] <= x <= e["XMax"] and e["YMin"] <= y <= e["YMax"]]
        born_torches = [i for i, e in enumerate(d["SpriteInfo"]["Entities"]["Entities"])
                        if e and e["IsEnabled"] and (e["SpriteDirection"] & 0x40) and e["SpriteTableIndex"] == 137
                        and e["XMin"] <= x <= e["XMax"] and e["YMin"] <= y <= e["YMax"]]
        extra = ""
        if target == 92:
            extra = " in B1 zone(26..42,8..28)=%s" % (26 <= x <= 42 and 8 <= y <= 28)
        print("   from %-45s tile (%d,%d): drops born %s torches born %s%s" % (n, x, y, born_drops, born_torches, extra))
