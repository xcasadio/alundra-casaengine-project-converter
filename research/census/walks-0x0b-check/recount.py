"""recount.py - recount the census's headline numbers from table.tsv (and the rows pickles) with the checker's own code."""
import collections
import csv
import os
import sys

sys.stdout.reconfigure(encoding="utf-8")
HERE = os.path.dirname(os.path.abspath(__file__))
C0 = os.path.join(os.path.dirname(HERE), "census0b")
tab = list(csv.DictReader(open(C0 + "/table.tsv", encoding="utf-8"), delimiter="\t"))
R = [r for r in tab if r["reach"] == "reachable"]
print("reachable", len(R), "maps", len({r["map"] for r in R}))
cls = collections.Counter(r["cls"] for r in R)
print(sorted(cls.items(), key=lambda x: -x[1]), sum(cls.values()))

dllonly = [r for r in R if r["cls"] == "blocked in DLL only"]
print("blocked in DLL only:", len(dllonly), collections.Counter(r["mech"] for r in dllonly))
print("  story:", sum(1 for r in dllonly if r["story"] == "True"), " held:", sum(1 for r in dllonly if r["lock"] == "True"))
rel_fix = [r for r in dllonly if r["rel"].startswith("released by FIX") and "0x28" not in r["rel"]]
rel_fixc = [r for r in dllonly if r["v_FIXC"] == "P" and not r["rel"].startswith("released by FIX [emu]") and r["v_FIX"] != "P"]
print("  released by FIX:", len(rel_fix), [(r["map"], r["pc"]) for r in rel_fix])
print("  of them on story maps:", sum(1 for r in rel_fix if r["story"] == "True"))
print("  FIXC == P but FIX != P:", len(rel_fixc), [(r["map"], r["pc"], r["mech"]) for r in rel_fixc])
co = [r for r in dllonly if r["mech"] == "class-op"]
print("  class-op rows:", len(co), " FIXC verdicts:", collections.Counter(r["v_FIXC"] for r in co))
print("  class-op by map:", collections.Counter(r["map"] for r in co))
some = [r for r in R if r["cls"] == "blocked in DLL only (some starts)"]
print("blocked in DLL only (some starts):", len(some), collections.Counter(r["mech"] for r in some),
      " FIX P:", [(r["map"], r["pc"]) for r in some if r["v_FIX"] == "P"], " FIXC P:", [(r["map"], r["pc"]) for r in some if r["v_FIXC"] == "P"])

down = [r for r in R if r["cls"] == "downstream"]
print("downstream:", len(down), " FIX reaches:", sum(1 for r in down if not r["v_FIX"].startswith("U")),
      " FIXC reaches:", sum(1 for r in down if not r["v_FIXC"].startswith("U")),
      " FIX reaches and passes:", sum(1 for r in down if r["v_FIX"] == "P"),
      " FIXC reaches and passes:", sum(1 for r in down if r["v_FIXC"] == "P"))
print("  FIX verdicts on downstream:", collections.Counter(r["v_FIX"] for r in down))
print("  FIXC verdicts on downstream:", collections.Counter(r["v_FIXC"] for r in down))

# new blocks: DLL P and FIX in (B, M) / FIXC in (B, M)
nb = [r for r in R if r["v_DLL"] == "P" and r["v_FIX"] in ("B", "M")]
nbc = [r for r in R if r["v_DLL"] == "P" and r["v_FIXC"] in ("B", "M")]
print("NEW blocks FIX:", len(nb), [(r["map"], r["pc"], r["v_FIX"]) for r in nb])
print("NEW blocks FIXC:", len(nbc), [(r["map"], r["pc"], r["v_FIXC"]) for r in nbc])
nbi = [r for r in R if r["v_DLL"] == "P" and ("B" in r["v_FIXC"] and r["v_FIXC"].startswith("I"))]
print("FIXC I+..B over DLL P:", [(r["map"], r["pc"], r["v_FIXC"]) for r in nbi])
# FIX worse than DLL on mixed
worse = [r for r in R if r["v_DLL"] in ("M",) and r["v_FIX"] == "B"]
print("DLL M -> FIX B:", [(r["map"], r["pc"]) for r in worse])
# rows where DLL P and FIX U:walk (the fix cuts a site the DLL reaches)
cut = [r for r in R if not r["v_DLL"].startswith("U") and r["v_FIX"].startswith("U")]
print("DLL reaches, FIX does not:", len(cut), collections.Counter(r["map"] for r in cut))
cutc = [r for r in R if not r["v_DLL"].startswith("U") and r["v_FIXC"].startswith("U")]
print("DLL reaches, FIXC does not:", len(cutc), collections.Counter(r["map"] for r in cutc))
oo = [r for r in R if r["cls"] == "blocked in original only"]
print("blocked in original only:", len(oo), collections.Counter(r["mech"] for r in oo))
oos = [r for r in R if r["cls"] == "blocked in original only (some starts)"]
print("blocked in original only (some starts):", len(oos), collections.Counter(r["mech"] for r in oos),
      " FIX in B/M:", [(r["map"], r["pc"], r["v_FIX"]) for r in oos if r["v_FIX"] in ("B", "M")])
bb = [r for r in R if r["cls"] == "blocked in both"]
print("blocked in both:", len(bb), " story:", [(r["map"], r["pc"]) for r in bb if r["story"] == "True"])
print("story difference rows:")
for r in R:
    if r["story"] == "True" and r["cls"] not in ("passes in both", "not reached"):
        print("   ", r["map"], r["pc"], r["op"], r["cls"], r["mech"], r["actor"], "/".join(r["v_" + k] for k in ("DLL", "ORIG0", "ORIGnc", "ORIG", "FIX", "FIXC")), r["lock"], r["flags"])
