"""pins.py - model values for the re-classified sites (mysim, census start states), header flags vs flags after the program's
own 0x63 / 0x2B."""
import os
import pickle
import sys

sys.stdout.reconfigure(encoding="utf-8")
HERE = os.path.dirname(os.path.abspath(__file__))
C0 = os.path.join(os.path.dirname(HERE), "census0b")
sys.path.insert(0, HERE)
import mysim as S  # noqa: E402
from compare import mapdata, pid_of  # noqa: E402

rows = {(r["map"], r["pc"]): r for r in pickle.load(open(C0 + "/rows_DLL.pkl", "rb"))}
CASES = [((15, 722), 0x0001), ((17, 3454), 0x0001), ((154, 1088), 0x2081), ((154, 1415), 0x2081), ((393, 190), 0x0001),
         ((393, 246), 0x0001), ((439, 3454), 0x0001), ((15, 665), 0x0001)]
for (k, clear) in CASES:
    F, recs = mapdata(k[0])
    for cls, d in rows[k]["all"]:
        h = S.SPR[pid_of(k[0], d["key"])]
        X0, Y0 = int(round(d["pin"][0] * S.U)), int(round(d["pin"][1] * S.U))
        out = []
        for name, fl in (("header", S.flags_of(h)), ("cleared", S.flags_of(h) & ~clear)):
            b = S.Body(h, flags=fl)
            kd, xd, yd, wd = S.walk_dll(F, b, X0, Y0, d["dir"], d["rad"])
            ko, xo, yo, wo = S.walk_orig(F, b, X0, Y0, d["dir"], d["rad"])
            ko2, xo2, yo2, _ = S.walk_orig(F, b, X0, Y0, d["dir"], d["rad"], oblique_slide=False)
            out.append("%s mask 0x%X: DLL %s (%.3f,%.3f) %s | ORIG %s (%.3f,%.3f) | ORIG no-oblique %s" % (
                name, S.mask_of(fl), kd, xd / S.U, yd / S.U, wd or "", ko, xo / S.U, yo / S.U, ko2))
        print(k, d["actor"], "dir", d["dir"], "pin", d["pin"], "r", d["rad"])
        for o in out:
            print("     ", o)
