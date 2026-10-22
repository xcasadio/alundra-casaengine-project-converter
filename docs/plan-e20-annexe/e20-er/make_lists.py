"""Builds the stable lists the plan quotes (relative paths, sorted, LF), from the dry-run diff and the prediction TSV."""
import hashlib
import re
import sys

S = sys.argv[1]
rel = []
for line in open(f"{S}/diff-new-vs-dataextracted.txt", encoding="utf-8"):
    m = re.search(r"/extract-new/(data/tiled/map_\d+\.tmj) and ", line.replace("\\", "/"))
    rel.append(m.group(1))
rel.sort()
text = "".join("M " + r + "\n" for r in rel)
open(f"{S}/expected_extract_changes.txt", "w", encoding="utf-8", newline="\n").write(text)
print("expected_extract_changes.txt:", len(rel), "lines, sha1", hashlib.sha1(text.encode()).hexdigest())

raw = open(f"{S}/predicted_export_changes.tsv", "rb").read()
print("predicted_export_changes.tsv:", len(raw), "bytes, sha1", hashlib.sha1(raw).hexdigest())
rows = [l.split("\t") for l in raw.decode().splitlines()[1:]]
paths = "".join("M " + r[0] + "\n" for r in rows)
print("paths list:", len(rows), "sha1", hashlib.sha1(paths.encode()).hexdigest())
print("tmj:", sum(1 for r in rows if r[0].endswith(".tmj")), "tileMap:", sum(1 for r in rows if r[0].endswith(".tileMap")))
