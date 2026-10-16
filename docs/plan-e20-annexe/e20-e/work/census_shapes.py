"""Shapes of the wall stacks of the cuttable cells (export alundra-project, AlundraCells). Read-only."""
import json, glob, os, re, collections
R = r"D:\development\repo\alundra-casaengine-project-converter\alundra-project\Maps"
OUT = os.path.dirname(os.path.abspath(__file__))
shape = collections.Counter(); top = collections.Counter(); cnt = collections.Counter(); off = collections.Counter()
maps_by_shape = collections.defaultdict(set)
noflag_stack = 0  # cells with a wall stack and NOT flagged (for comparison)
flagged_total = 0
for f in glob.glob(os.path.join(R, "*", "*", "tilemap", "*.tileMap")):
    mid = int(re.search(r"-(\d+)\.tileMap$", f).group(1))
    cp = json.load(open(f, encoding="utf-8"))["custom_properties"]
    cells = json.loads(cp["AlundraCells"])
    walk = cells["walkability"]; stacks = cells.get("wall_tiles", {})
    for i, w in enumerate(walk):
        s = stacks.get(str(i))
        if w & 2:
            flagged_total += 1
            if s:
                t = tuple(x for x in s["tiles"] if x != 0xffff)
                shape[t] += 1; cnt[len(s["tiles"])] += 1; off[s["offset"]] += 1
                for x in t: top[x] += 1
                maps_by_shape[t].add(mid)
        elif s:
            noflag_stack += 1
print("flagged cells:", flagged_total, "unflagged cells that have a stack:", noflag_stack)
print("stack length distribution (cuttable cells):", dict(sorted(cnt.items())))
print("offset distribution:", dict(sorted(off.items())))
print("distinct stacks:", len(shape))
print("top stacks (tuple of raw tile ids -> cells, maps):")
for t, n in shape.most_common(12):
    print("  ", t, n, len(maps_by_shape[t]))
print("distinct raw tile ids in cuttable stacks:", len(top))
json.dump({"stack_len": cnt, "offsets": off}, open(os.path.join(OUT, "census_shapes.json"), "w"), default=str, indent=1)
