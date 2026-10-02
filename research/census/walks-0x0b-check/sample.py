"""sample.py - 20 random reachable rows (seed 19), outside the story maps and held control: own listing of the 12 instructions
before the site, census states (actor, dir, anim, pin, radius, outcome), so the direction / animation / radius / actor can be read
against the program by hand."""
import csv
import json
import os
import pickle
import random
import sys

sys.stdout.reconfigure(encoding="utf-8")
HERE = os.path.dirname(os.path.abspath(__file__))
C0 = os.path.join(os.path.dirname(HERE), "census0b")
sys.path.insert(0, HERE)
from pop import maps, SZ_BIN  # noqa: E402

M = maps()
tab = [r for r in csv.DictReader(open(C0 + "/table.tsv", encoding="utf-8"), delimiter="\t")]
rows = {(r["map"], r["pc"]): r for r in pickle.load(open(C0 + "/rows_DLL.pkl", "rb"))}
pool = [r for r in tab if r["reach"] == "reachable" and r["story"] == "False" and r["lock"] == "False"
        and rows[(int(r["map"]), int(r["pc"]))].get("reached")]
random.seed(19)
pick = random.sample(pool, 20)
for r in sorted(pick, key=lambda x: (int(x["map"]), int(x["pc"]))):
    mid, pc = int(r["map"]), int(r["pc"])
    ev = json.load(open(M[mid][1], encoding="utf-8"))
    C = ev["Codes"]
    T = {k: ev["EventCodes%sTable" % k] for k in "ABCDEF"}
    lin = []
    q = sum(2 * len(t) for t in T.values())
    while q < len(C):
        lin.append(q)
        q += SZ_BIN.get(C[q]) or 1
    i = lin.index(pc)
    print("=== map %d @%d %s cls=%s  DLL=%s ORIG=%s roots=%s" % (mid, pc, r["op"], r["cls"], r["v_DLL"], r["v_ORIG"], rows[(mid, pc)]["roots"]))
    for p in lin[max(0, i - 10):i + 1]:
        print("     %5d: %02X %s" % (p, C[p], C[p:p + (SZ_BIN.get(C[p]) or 1)][1:]))
    seen = set()
    for cls, d in rows[(mid, pc)]["all"]:
        s = (cls, d.get("actor"), d.get("dir"), d.get("anim"), d.get("pin"), d.get("rad"), d.get("n"))
        if s in seen:
            continue
        seen.add(s)
        if len(seen) > 4:
            break
        print("     state:", s)
