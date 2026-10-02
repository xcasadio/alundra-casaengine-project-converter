"""oblique.py - how much of the census rests on oblique (non-cardinal) walk directions, which the census's ORIG rule never
slides, while the binary's DECIDE_FINAL_OBSTACLE has slide cases for every oblique direction (PhysicsEngine.cs:600-810,
cases 1-7, 9-15, 17-23, 25-31: zero one FinalForce component and retry)."""
import collections
import csv
import os
import pickle
import sys

sys.stdout.reconfigure(encoding="utf-8")
HERE = os.path.dirname(os.path.abspath(__file__))
C0 = os.path.join(os.path.dirname(HERE), "census0b")


def load(tag):
    return {(r["map"], r["pc"]): r for r in pickle.load(open(C0 + "/rows_%s.pkl" % tag, "rb"))}


runs = {t: load(t) for t in ("DLL", "ORIG0", "ORIG", "FIX")}
tab = {(int(r["map"]), int(r["pc"])): r for r in csv.DictReader(open(C0 + "/table.tsv", encoding="utf-8"), delimiter="\t")}

for t, rows in runs.items():
    c = collections.Counter()
    for k, r in rows.items():
        for cls, det in r.get("all", []) or []:
            d = det.get("dir")
            if d is None:
                c["nodir"] += 1
                continue
            ob = (d % 8) != 0
            c[("oblique" if ob else "cardinal", cls)] += 1
    print(t, sorted(c.items(), key=lambda x: str(x[0])))

# sites where an ORIG-family run blocks an oblique state while the DLL does not block it (same start)
print()
print("difference-class rows with oblique blocked states (ORIG0):")
for k, row in sorted(tab.items()):
    cls = row["cls"]
    if cls in ("passes in both", "not reached", "dormant", "uncertain", "downstream"):
        continue
    r0 = runs["ORIG0"].get(k)
    rd = runs["DLL"].get(k)
    ob0 = sorted({det["dir"] for c, det in (r0.get("all") or []) if c == "blocked" and det.get("dir") is not None and det["dir"] % 8})
    obd = sorted({det["dir"] for c, det in (rd.get("all") or []) if c == "blocked" and det.get("dir") is not None and det["dir"] % 8})
    alld = sorted({det["dir"] for c, det in (r0.get("all") or []) if det.get("dir") is not None})
    if ob0 or obd:
        print("  %4d @%-5d %-40s %-25s dirs %s ORIG0-blocked-oblique %s DLL-blocked-oblique %s" % (
            k[0], k[1], cls, row["mech"], alld, ob0, obd))
