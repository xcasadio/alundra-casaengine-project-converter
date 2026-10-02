"""samestart.py - for the difference classes, are the blocked states of one run states that the other run also has (same actor,
anim, dir, pin, radius, root)? rep.py's klass() only uses per-run verdict letters outside the M/M case, so a
'blocked in original only' row can rest on starts that only the ORIG run produces (and vice versa)."""
import collections
import csv
import os
import pickle
import sys

sys.stdout.reconfigure(encoding="utf-8")
HERE = os.path.dirname(os.path.abspath(__file__))
C0 = os.path.join(os.path.dirname(HERE), "census0b")
tab = {(int(r["map"]), int(r["pc"])): r for r in csv.DictReader(open(C0 + "/table.tsv", encoding="utf-8"), delimiter="\t")}
R = {t: {(r["map"], r["pc"]): r for r in pickle.load(open(C0 + "/rows_%s.pkl" % t, "rb"))} for t in ("DLL", "ORIG")}


def sk(d):
    return (repr(d.get("key")), d.get("anim"), d.get("dir"), d.get("pin"), d.get("rad"), d.get("root"))


for cls, blk, oth in (("blocked in DLL only", "DLL", "ORIG"), ("blocked in original only", "ORIG", "DLL")):
    agg = collections.Counter()
    for k, row in sorted(tab.items()):
        if row["cls"] != cls or row["note"]:
            continue
        a = [sk(d) for c, d in (R[blk][k].get("all") or []) if c == "blocked"]
        b = {sk(d) for c, d in (R[oth][k].get("all") or [])}
        shared = sum(1 for s in a if s in b)
        tag = "all blocked starts shared" if shared == len(a) else ("none shared" if shared == 0 else "partly shared")
        agg[tag] += 1
        if tag != "all blocked starts shared":
            print("  %-26s %4d @%-5d %-8s %s blocked states %d, shared with %s %d" % (cls, k[0], k[1], row["mech"], blk, len(a), oth, shared))
    print(cls, dict(agg))
