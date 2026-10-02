"""down.py - 'not reached' sites that sit after a given site in the same program (rows' roots), for the sites the checker
re-classifies."""
import csv
import os
import pickle
import sys

sys.stdout.reconfigure(encoding="utf-8")
HERE = os.path.dirname(os.path.abspath(__file__))
C0 = os.path.join(os.path.dirname(HERE), "census0b")
tab = {(int(r["map"]), int(r["pc"])): r for r in csv.DictReader(open(C0 + "/table.tsv", encoding="utf-8"), delimiter="\t")}
rows = {(r["map"], r["pc"]): r for r in pickle.load(open(C0 + "/rows_DLL.pkl", "rb"))}
SITES = [(15, 722), (17, 3454), (154, 1088), (154, 1415), (393, 190), (393, 246), (439, 3454), (1, 702), (15, 665), (1, 634),
         (150, 692), (369, 162), (382, 798), (382, 934)]
for s in SITES:
    roots = set(rows[s]["roots"])
    after = [k for k, r in sorted(rows.items()) if k[0] == s[0] and k[1] > s[1] and roots & set(r.get("roots", []))
             and tab[k]["cls"] in ("not reached", "uncertain", "blocked in both")]
    print(s, tab[s]["cls"], "->", [(k[1], tab[k]["cls"], tab[k]["v_DLL"], str(tab[k]["dll_unreached"])[:40]) for k in after])
