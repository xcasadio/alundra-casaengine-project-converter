"""studyF.py - O-E20-24 (point blank arrow) predicted by the model of 0x80037730 alone, against the frozen oracle table of the t2-proj report
(`pb_sweep_east.txt`, hunter arrow type 3, hero facing east, wall column 5 = cells with attribute 0x40 from x >= 120; the arrow is born at the hero's x0, moves 3 px at its
birth tick S and 6 px per tick after (the formula x0 + 3 + 6k of ArrowLifeTests.E7)).  Oracle 'adj@' = first tick (counted from S) with +0x13C raised; xS = PosX after tick S.
"""
import sys, os
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import xyscen as SC
from xyscen import U

SUBJ = SC.ent_addr(1)
ORACLE_ADJ = {100: 2, 101: 3, 102: 2, 103: 2, 104: 3, 105: 2, 106: 1, 107: 2, 108: 1, 109: 0}
ORACLE_XS = {100: 103, 101: 104, 102: 105, 103: 106, 104: 107, 105: 108, 106: 109, 107: 109, 108: 109, 109: 109}


def one(x0):
    w = SC.new_world()
    SC.wall_columns(w, 5)
    SC.put(w, SUBJ, 0, (x0 * U, 100 * U, 1), d=24)
    r = SC.tick(w, SUBJ, 3 * U, 0)
    xs = r["x"]
    fa_at = 0 if r["fa"] else None
    k = 0
    while fa_at is None:
        k += 1
        r = SC.tick(w, SUBJ, 6 * U, 0)
        if r["fa"]:
            fa_at = k
    return xs, fa_at, r["x"]


lines = ["x0   xS(model, px)   xS(oracle)   adj@(model)  adj@(oracle)  x@adj(model)  agree"]
agree = 0
for x0 in range(100, 110):
    xs, fa, xf = one(x0)
    ok = (fa == ORACLE_ADJ[x0]) and (abs(xs / U - ORACLE_XS[x0]) < 0.001)
    agree += ok
    lines.append("%d  %14.5f  %10d  %11d  %12d  %12.5f  %s" % (x0, xs / U, ORACLE_XS[x0], fa, ORACLE_ADJ[x0], xf / U, "yes" if ok else "NO"))
lines.append("agreement with the oracle table: %d / 10" % agree)
out = "\n".join(lines)
print(out)
open(os.path.join(HERE, "..", "out", "studyF.txt"), "w", encoding="utf-8").write(out + "\n")
