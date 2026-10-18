"""Linear check of the sm2 memo reads: for every 0x6B/0x6C/0x6D site of skipped-sites.tsv, decode its program linearly from
its entry, find the last 0x6A before the read, and list the instructions between them that write the axis per the binary
writer sets (check.md 2.4): +0xC (X): 0B 1C 1D 1E 1F 20 21 22 23 37 5F BB 6A; +0x10 (Y): 0B 1E 1F 5F 6A; +0x14 (Z): 5F 6A.
Also lists the 0x78/0x7B/0x7C calls between them (their callees are not followed here)."""
import csv, json, sys, os
sys.path.insert(0, os.path.dirname(__file__))
from v_decode import sizes, find
REPO = "D:/development/repo/alundra-casaengine-project-converter"
W = {0x6B: {0x0B, 0x1C, 0x1D, 0x1E, 0x1F, 0x20, 0x21, 0x22, 0x23, 0x37, 0x5F, 0xBB},
     0x6C: {0x0B, 0x1E, 0x1F, 0x5F},
     0x6D: {0x5F}}
S = sizes()
rows = list(csv.DictReader(open(REPO + "/docs/plan-e19-sm-annexe/skipped-sites.tsv", encoding="utf-8"), delimiter="\t"))
reads = [r for r in rows if r["opcode"].upper() in ("6B", "6C", "6D")]
cache = {}
stats = {"reads": 0, "with6A": 0, "no6A": 0, "clobbered": 0, "calls_between": 0}
for r in reads:
    m, slot, prog, pc = int(r["map"]), r["slot"], int(r["program"]), int(r["pc"])
    if m not in cache:
        cache[m] = json.load(open(find(m), encoding="utf-8"))
    d = cache[m]
    codes = d["Codes"]
    start = d["EventCodes%sTable" % slot][prog]
    seq = []
    p = start
    while p < len(codes):
        op = codes[p]
        sz = S.get(op, (0, ""))[0]
        seq.append((p, op))
        if p == pc or op == 0xFF or sz == 0:
            break
        p += sz
    stats["reads"] += 1
    if not seq or seq[-1][0] != pc:
        print("NOT REACHED LINEARLY", m, slot, prog, pc)
        continue
    idx6a = max((i for i, (q, op) in enumerate(seq) if op == 0x6A), default=None)
    if idx6a is None:
        stats["no6A"] += 1
        between = seq[:-1]
        tag = "no 6A"
    else:
        stats["with6A"] += 1
        between = seq[idx6a + 1:-1]
        tag = "6A@%d" % seq[idx6a][0]
    op = int(r["opcode"], 16)
    clob = [(q, "%02X" % o) for q, o in between if o in W[op]]
    calls = [(q, "%02X" % o) for q, o in between if o in (0x78, 0x7B, 0x7C)]
    if clob and idx6a is not None:
        stats["clobbered"] += 1
    if calls:
        stats["calls_between"] += 1
    if clob or calls or idx6a is None:
        print(m, slot, prog, pc, "%02X" % op, tag, "writers:", clob, "calls:", calls)
print(stats)
