"""map22.py - read-only facts about the elevator of map 22 (Lars' Crypt): records, cells around the shaft, portals into the area."""
import sys, os, json
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(HERE, "annex", "work", "h2tools"))
import m
sys.stdout.reconfigure(encoding="utf-8")

mp = m.Map(22)
SPR = json.load(open(m.ROOT + "/alundra-project/Data/sprite-records.json", encoding="utf-8-sig"))
print("records (index, name, px, py, z px, tile, spawned, size):")
for r in mp.records:
    h = SPR.get(r.get("PrefabAssetId"))
    px = int(r["XPos"]) * 12 + 12
    py = int(r["YPos"]) * 8 + 8
    pz = int(r["Height"]) * 8
    sp = int(r["IsEnabled"]) != 0 and (int(r["SpriteDirection"]) & 0x40) != 0
    if r["Index"] in ("0", "1", "2") or 30 <= px // 24 <= 50 and 3 <= py // 16 <= 16:
        print(" ", r["Index"], r["EntityName"], "px", px, py, "z", pz, "tile", px // 24, py // 16, "spawned", sp,
              "size", (h["SizeX"], h["SizeY"], h["SizeZ"]) if h else None, "off", (h["OffsetX"], h["OffsetY"], h["OffsetZ"]) if h else None)
print()
for name in ("height", "walkability", "ground_property"):
    print(name, "(hex), columns 34..49, rows 4..15")
    print("     " + " ".join("%2d" % x for x in range(34, 50)))
    for y in range(4, 16):
        print("%2d:  " % y + " ".join("%2x" % mp.cell(name, x, y) for x in range(34, 50)))
print()
print("portals whose destination is in the area (x 30..50, y 5..16) of map 22:")
for p in mp.portals:
    if p["DestMapId"] == "22" and 30 <= int(p["DestTileX"]) <= 50 and 5 <= int(p["DestTileY"]) <= 16:
        print(" ", p)
print("portal tile -> where the source rectangles are:")
for p in mp.portals:
    print(" ", p["Index"], "src", (p["X1"], p["Y1"], p["X2"], p["Y2"]), "->", p["DestMapId"], (p["DestTileX"], p["DestTileY"]), "Z", p["ZLevel"], "flags", p["Flags"])
