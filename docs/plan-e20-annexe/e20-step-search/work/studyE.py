"""studyE.py - the 12 starts of ArrowLifeTests.E7 / ChargedArrowLifeTests.C7 (frozen oracle keys: the flag is one tick LATE for x0 in {98,101,104,107}),
predicted by the model of 0x80037730 alone.  Arrow: PosX = x0 + 3 at tick S, then +6 px per tick (the test's own formula x0 + 3 + 6k), exact contact PosX = 349.
Test's DLL expectation: flag = arrival + 1 with arrival = first k with x0 + 3 + 6k >= 349 (the engine stops AT the contact on that tick).
Prediction of the model: the binary's flag tick k_bin; 'late' = k_bin - (arrival + 1).  Expected from the keys: late(x0) == 1 exactly for x0 in {98,101,104,107}.
"""
import sys, os
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import xyscen as SC
from xyscen import U

SUBJ = SC.ent_addr(1)
LATE_KEYS = {98, 101, 104, 107}


def flag_tick(x0, force_px=6, spawn=3):
    w = SC.new_world()
    SC.wall_columns(w, 15)
    SC.put(w, SUBJ, 0, ((x0 + spawn) * U, 100 * U, 1), d=24)
    for k in range(1, 90):                       # tick S+k moves by 6 px (tick S is the birth: already at x0+3)
        r = SC.tick(w, SUBJ, force_px * U, 0)
        if r["fa"]:
            return k
    return None


lines = ["x0   arrival(test)  flag S+k (model)  late (model)  late (keys)  agree"]
agree = 0
for x0 in range(96, 108):
    arrival = (349 - x0 - 3 + 5) // 6
    k = flag_tick(x0)
    late = k - (arrival + 1)
    ok = (late == 1) == (x0 in LATE_KEYS) and late in (0, 1)
    agree += ok
    lines.append("%d  %8d  %14d  %12d  %11d  %s" % (x0, arrival, k, late, 1 if x0 in LATE_KEYS else 0, "yes" if ok else "NO"))
lines.append("agreement with the frozen oracle keys: %d / 12" % agree)
out = "\n".join(lines)
print(out)
open(os.path.join(HERE, "..", "out", "studyE.txt"), "w", encoding="utf-8").write(out + "\n")
