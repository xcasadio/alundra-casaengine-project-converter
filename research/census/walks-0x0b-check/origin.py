"""origin.py - states that start at (0, 0) (the 0x64 [0x80, 0, 0, 0, 0, ...] 'park and destroy' write used as a start candidate)
and their weight in the difference classes."""
import collections
import csv
import os
import pickle
import sys

sys.stdout.reconfigure(encoding="utf-8")
HERE = os.path.dirname(os.path.abspath(__file__))
C0 = os.path.join(os.path.dirname(HERE), "census0b")
tab = {(int(r["map"]), int(r["pc"])): r for r in csv.DictReader(open(C0 + "/table.tsv", encoding="utf-8"), delimiter="\t")}
for t in ("DLL", "ORIG"):
    rows = {(r["map"], r["pc"]): r for r in pickle.load(open(C0 + "/rows_%s.pkl" % t, "rb"))}
    c = collections.Counter()
    only = collections.Counter()
    for k, r in rows.items():
        sts = r.get("all") or []
        z = [cls for cls, d in sts if d.get("pin") is not None and tuple(d["pin"]) == (0, 0)]
        if z:
            c[(tab[k]["cls"], tuple(sorted(set(z))))] += 1
            if len(z) == len([1 for cls, d in sts if d.get("pin") is not None]):
                only[tab[k]["cls"]] += 1
    print(t, "sites with a (0,0) start state, by class / outcomes of those states:")
    for k, n in sorted(c.items(), key=lambda x: -x[1]):
        print("   %4d %s" % (n, k))
    print(t, "sites whose ONLY positioned states start at (0,0):", dict(only))
print()
print("difference-class rows with a (0,0) state in DLL or ORIG:")
rowsD = {(r["map"], r["pc"]): r for r in pickle.load(open(C0 + "/rows_DLL.pkl", "rb"))}
rowsO = {(r["map"], r["pc"]): r for r in pickle.load(open(C0 + "/rows_ORIG.pkl", "rb"))}
for k, row in sorted(tab.items()):
    if row["cls"] in ("passes in both", "not reached", "dormant", "downstream", "uncertain"):
        continue
    zs = []
    for t, rr in (("DLL", rowsD), ("ORIG", rowsO)):
        for cls, d in (rr[k].get("all") or []):
            if d.get("pin") is not None and tuple(d["pin"]) == (0, 0):
                zs.append((t, cls))
    if zs:
        print("  %4d @%-5d %-40s %s" % (k[0], k[1], row["cls"], sorted(set(zs))))
