"""Independent check: size of what a corpus test reads (tileMap and events files of the 483 maps)."""
import json
import os
import sys

root = sys.argv[1] + "/alundra-project"
idx = json.load(open(root + "/Maps/world-index.json", encoding="utf-8"))
tm = ev = mx = n = multi = 0
for k, rel in idx.items():
    rel = rel.replace(chr(92), "/")
    folder = os.path.dirname(root + "/" + rel)
    base = os.path.splitext(os.path.basename(rel))[0]
    t = [f for f in os.listdir(folder + "/tilemap") if f.endswith(".tileMap")]
    if len(t) != 1:
        multi += 1
    s = os.path.getsize(folder + "/tilemap/" + t[0])
    tm += s
    mx = max(mx, s)
    ev += os.path.getsize(folder + "/events/" + base + ".events.json")
    n += 1
print(n, "maps;", round(tm / 1e6, 1), "MB tileMap (max", round(mx / 1e6, 2), "MB);", round(ev / 1e6, 1), "MB events; maps with !=1 tileMap:", multi)
