"""Cross-check of the census on the EXPORT (alundra-project/Maps/**/tilemap/*.tileMap, custom properties AlundraCells + AlundraWallPlacements). Read-only.
For every map: cells with walkability & 2 ('cuttable'), how many have a wall stack in AlundraCells, how many of their stack tiles are listed in AlundraWallPlacements (i.e. drawn by the sorted overlay, hence removable by the E7 sync)."""
import json, glob, os, re, collections
R = r"D:\development\repo\alundra-casaengine-project-converter\alundra-project\Maps"
OUT = os.path.dirname(os.path.abspath(__file__))
files = glob.glob(os.path.join(R, "*", "*", "tilemap", "*.tileMap"))
rows = []
tot = collections.Counter()
for f in files:
    m = re.search(r"-(\d+)\.tileMap$", f)
    if not m: continue
    mid = int(m.group(1))
    d = json.load(open(f, encoding="utf-8"))
    cp = d["custom_properties"]
    cells = json.loads(cp["AlundraCells"])
    wp = json.loads(cp["AlundraWallPlacements"]) if "AlundraWallPlacements" in cp else None
    walk = cells["walkability"]
    n = sum(1 for w in walk if w & 2)
    stacks = cells.get("wall_tiles", {})
    placed = collections.Counter()
    if wp:
        for cx, cy, k in zip(wp["cell_x"], wp["cell_y"], wp["stack_index"]):
            placed[cy * (cells["cell_count"] // 60 if False else 52) + cx] += 1
    nstack = 0; ntiles = 0; ntiles_placed = 0; cells_fully_placed = 0
    for i, w in enumerate(walk):
        if w & 2:
            s = stacks.get(str(i))
            if s:
                nstack += 1
                t = [x for x in s["tiles"] if x != 0xffff]
                ntiles += len(t)
                p = placed.get(i, 0)
                ntiles_placed += min(p, len(t))
                if p >= len(t): cells_fully_placed += 1
    rows.append(dict(map=mid, cuttable=n, with_stack=nstack, visible_tiles=ntiles, tiles_in_overlay=ntiles_placed, cells_fully_in_overlay=cells_fully_placed))
    tot.update(cuttable=n, with_stack=nstack, visible_tiles=ntiles, tiles_in_overlay=ntiles_placed, cells_fully_in_overlay=cells_fully_placed)
rows.sort(key=lambda r: r["map"])
json.dump(rows, open(os.path.join(OUT, "census_export.json"), "w"), indent=1)
print("tileMap files:", len(files), "maps with cuttable:", sum(1 for r in rows if r["cuttable"]), dict(tot))
