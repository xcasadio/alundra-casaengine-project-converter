"""Read-only prediction of the E20.eR export diff (nothing is written outside the scratchpad).

usage: python -I predict_export.py <project-dir> <data-extracted> <new-extraction> <out-tsv>

For every map: the project .tmj must be a byte copy of data-extracted/data/tiled/map_N.tmj (checked), so its predicted new
bytes are the new extraction's map_N.tmj. The project .tileMap is predicted by renaming the map custom property key
('"BalanceLevel": "2"' -> '"TileCutEffectId": "2"', exactly once). Prints the paths, sizes and predicted SHA-1 into <out-tsv>.
"""
import hashlib
import os
import re
import sys

project, old_root, new_root, out_tsv = sys.argv[1:5]
maps_dir = os.path.join(project, "Maps")
rows = []
not_copy = []
bad_tilemap = []
n_tmj = n_tilemap = 0
tmj_name = re.compile(r"-(\d+)\.tmj$")
for dirpath, _dirs, files in os.walk(maps_dir):
    for name in files:
        full = os.path.join(dirpath, name)
        rel = os.path.relpath(full, project).replace(os.sep, "/")
        if name.endswith(".tmj"):
            m = tmj_name.search(name)
            n = int(m.group(1))
            cur = open(full, "rb").read()
            src_old = open(f"{old_root}/data/tiled/map_{n}.tmj", "rb").read()
            src_new = open(f"{new_root}/data/tiled/map_{n}.tmj", "rb").read()
            if cur != src_old:
                not_copy.append(rel)
            n_tmj += 1
            rows.append((rel, len(cur), len(src_new), hashlib.sha1(cur).hexdigest(), hashlib.sha1(src_new).hexdigest()))
        elif name.endswith(".tileMap"):
            cur = open(full, "rb").read()
            key = b'"BalanceLevel": "2"'
            if cur.count(key) != 1 or cur.count(b"BalanceLevel") != 1:
                bad_tilemap.append((rel, cur.count(key), cur.count(b"BalanceLevel")))
                continue
            new = cur.replace(key, b'"TileCutEffectId": "2"')
            n_tilemap += 1
            rows.append((rel, len(cur), len(new), hashlib.sha1(cur).hexdigest(), hashlib.sha1(new).hexdigest()))
rows.sort()
with open(out_tsv, "w", encoding="utf-8", newline="\n") as f:
    f.write("path\told_size\tnew_size\told_sha1\tnew_sha1\n")
    for r in rows:
        f.write("\t".join(str(x) for x in r) + "\n")
print("tmj:", n_tmj, "tileMap:", n_tilemap, "rows:", len(rows))
print("project .tmj not a byte copy of the old source:", len(not_copy), not_copy[:3])
print("tileMap without exactly one BalanceLevel:", len(bad_tilemap), bad_tilemap[:3])
print("size delta total (bytes):", sum(r[2] - r[1] for r in rows))
print("distinct per-file size deltas:", sorted({r[2] - r[1] for r in rows}))
