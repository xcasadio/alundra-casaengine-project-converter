"""studyD.py - does the integer residue of the halving search explain the 'late flag' pattern of D-F4-9 / Q-K2 (8 starts on 12 at arrival+1, 4 at arrival+2,
late = x0 in {98, 101, 104, 107} for a 6 px arrow that stops at x = 349)?  Model: hero-sized box against a cell wall whose exact contact is x = 349.0,
force 6 px (or 3 px) per tick, start at 349 - D with D whole pixels, tick counted from the start.
"""
import sys, os
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import xyscen as SC
from xyscen import U

SUBJ = SC.ent_addr(1)
FMAX = 349 * U


def run(start, force, n=8):
    w = SC.new_world()
    # greatest free PosX = x_wall - 11 px  ->  x_wall = 360 (column 15) for FMAX = 349
    SC.wall_columns(w, 15)
    SC.put(w, SUBJ, 0, (start, 100 * U, 1), d=24)
    return [SC.tick(w, SUBJ, force, 0) for _ in range(n)]


lines = []
for force_px in (6, 3):
    F = force_px * U
    lines.append("force %d px per tick, wall exact contact at PosX = 349 px" % force_px)
    lines.append("  D(px)  arrival tick (PosX==contact)  first FA tick  FA - arrival")
    for Dpx in range(0, 3 * force_px + 1):
        r = run(FMAX - Dpx * U, F)
        arrival = next((i + 1 for i, t in enumerate(r) if t["x"] == FMAX), 0)
        fa = next((i + 1 for i, t in enumerate(r) if t["fa"]), 0)
        lines.append("  %5d  %27d  %13d  %12d   %s" % (Dpx, arrival, fa, fa - arrival, "LATE (+2)" if fa - arrival == 2 else ""))
    lines.append("")
out = "\n".join(lines)
print(out)
open(os.path.join(HERE, "..", "out", "studyD.txt"), "w", encoding="utf-8").write(out + "\n")
