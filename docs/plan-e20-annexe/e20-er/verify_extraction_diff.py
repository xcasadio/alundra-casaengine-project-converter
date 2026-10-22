"""Checks the dry-run extraction against data-extracted/ (read-only).

Usage: python -I verify_rename.py <diff-file> <new-root> <old-root>
Every line of the diff must be 'Files <new>/data/tiled/map_N.tmj and <old>/data/tiled/map_N.tmj differ',
for N in 0..482 exactly once, and the new file must equal the old one once the property name is put back.
"""
import re
import sys

diff_file, new_root, old_root = sys.argv[1:4]
pat = re.compile(r"^Files (.+) and (.+) differ$")
seen = []
other = []
for line in open(diff_file, encoding="utf-8", errors="replace"):
    line = line.rstrip("\n")
    m = pat.match(line)
    if not m:
        other.append(line)
        continue
    new_path, old_path = m.group(1), m.group(2)
    mm = re.search(r"data/tiled/map_(\d+)\.tmj$", new_path.replace("\\", "/"))
    if not mm:
        other.append(line)
        continue
    seen.append(int(mm.group(1)))

print("diff lines:", len(seen) + len(other))
print("tmj lines:", len(seen), "distinct:", len(set(seen)), "min/max:", min(seen), max(seen))
print("missing from 0..482:", sorted(set(range(483)) - set(seen)))
print("other lines:", len(other))
for o in other[:10]:
    print("  OTHER:", o)

bad = 0
delta_total = 0
for n in sorted(set(seen)):
    a = open(f"{new_root}/data/tiled/map_{n}.tmj", "rb").read()
    b = open(f"{old_root}/data/tiled/map_{n}.tmj", "rb").read()
    cnt_new = a.count(b'"name": "TileCutEffectId"')
    cnt_old = b.count(b'"name": "BalanceLevel"')
    cnt_old_in_new = a.count(b"BalanceLevel")
    back = a.replace(b'"name": "TileCutEffectId"', b'"name": "BalanceLevel"')
    ok = back == b and cnt_new == 1 and cnt_old == 1 and cnt_old_in_new == 0
    delta_total += len(a) - len(b)
    if not ok:
        bad += 1
        print("MISMATCH map", n, cnt_new, cnt_old, cnt_old_in_new, len(a), len(b))
print("maps whose only difference is the renamed property:", len(set(seen)) - bad, "of", len(set(seen)))
print("total size delta bytes:", delta_total)
# the value stays 2 everywhere
vals = set()
for n in range(483):
    a = open(f"{new_root}/data/tiled/map_{n}.tmj", "rb").read()
    m = re.search(rb'"name": "TileCutEffectId",\s*"type": "int",\s*"value": (\d+)', a)
    vals.add(m.group(1) if m else None)
print("values of TileCutEffectId over 483 maps:", vals)
