import csv, json, sys, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from v_decode import find, sizes
REPO = "D:/development/repo/alundra-casaengine-project-converter"
S = sizes()
rows = list(csv.DictReader(open(REPO + "/docs/plan-e19-sm-annexe/skipped-sites.tsv", encoding="utf-8"), delimiter="\t"))
sel = [r for r in rows if r["opcode"].upper() in ("60","61","3C","3D","6A","6B","6C","6D")]
cache = {}; bad = 0
for r in sel:
    m = int(r["map"])
    if m not in cache: cache[m] = json.load(open(find(m), encoding="utf-8"))
    c = cache[m]["Codes"]; pc = int(r["pc"]); op = int(r["opcode"], 16)
    sz = S[op][0]
    got = " ".join("%02X" % b for b in c[pc+1:pc+sz])
    if c[pc] != op or got != r["operands"]:
        bad += 1; print("MISMATCH", r)
print(len(sel), "sites checked,", bad, "mismatches")
