"""Size of the sorted overlay (wall + floor placements) of the maps with the most cuttable cells. Read-only."""
import json, glob, os, re
R = r"D:\development\repo\alundra-casaengine-project-converter\alundra-project\Maps"
rows = []
for f in glob.glob(os.path.join(R, "*", "*", "tilemap", "*.tileMap")):
    mid = int(re.search(r"-(\d+)\.tileMap$", f).group(1))
    cp = json.load(open(f, encoding="utf-8"))["custom_properties"]
    w = json.loads(cp["AlundraWallPlacements"])["count"] if "AlundraWallPlacements" in cp else 0
    fl = json.loads(cp["AlundraFloorPlacements"])["count"] if "AlundraFloorPlacements" in cp else 0
    cells = json.loads(cp["AlundraCells"])
    n = sum(1 for x in cells["walkability"] if x & 2)
    rows.append((mid, n, w, fl))
rows.sort(key=lambda r: -r[1])
print("map  cuttable  wallPlacements  floorPlacements")
for r in rows[:8]: print(*r)
print("max wall placements over all maps:", max(r[2] for r in rows), "max floor:", max(r[3] for r in rows))
print("max overlay entries (wall+floor):", max(r[2]+r[3] for r in rows))
