"""studyA2.py - the deficit of the halving search by direction: east/south (positive forces, shifts round toward zero) versus west/north (negative forces, the arithmetic shift
rounds toward minus infinity).  Cell wall; hero-sized box; exact contact positions are derived from the corner rule (floor(position + offset)) and checked by the model itself."""
import sys, os, collections
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import xyscen as SC
from xyscen import U

SUBJ = SC.ent_addr(1)


def setup(direction):
    w = SC.new_world()
    if direction == "east":      # wall columns >= 5 (x >= 120): greatest free PosX = 109.0
        SC.wall_columns(w, 5); d, ax, fmax = 24, 0, 109 * U
    elif direction == "west":    # wall columns <= 4 (x < 120): least free PosX = 130.0
        for c in range(0, 5):
            for r in range(0, 60):
                w.cell[(c, r)] = (0x40, 0, 0)
        d, ax, fmax = 8, 0, 130 * U
    elif direction == "south":   # wall rows >= 10 (y >= 160): greatest free PosY: floor(py + 8 - tiny) <= 159 -> 152.0
        for c in range(0, 52):
            for r in range(10, 60):
                w.cell[(c, r)] = (0x40, 0, 0)
        d, ax, fmax = 0, 1, 152 * U
    else:                        # north: wall rows <= 9 (y < 160): y1 = floor(py - 7) >= 160 -> least free PosY = 167.0
        for c in range(0, 52):
            for r in range(0, 10):
                w.cell[(c, r)] = (0x40, 0, 0)
        d, ax, fmax = 16, 1, 167 * U
    SC.put(w, SUBJ, 0, (300 * U, 300 * U, 1), d=d)
    return w, d, ax, fmax


lines = []
for direction in ("east", "west", "south", "north"):
    sign = 1 if direction in ("east", "south") else -1
    for F in (159744, 196608, 393216, 56473):
        deficits = collections.Counter()
        reach2 = 0
        n = 0
        step = max(1, F // 997)
        for D in list(range(1, F, step)) + list(range(1, 30)):
            w, d, ax, fmax = setup(direction)
            E = w.ents[SUBJ]
            E[0x114 + 4 * ax] = fmax - sign * D
            SC.XM.set_mod(E)
            fx, fy = (sign * F, 0) if ax == 0 else (0, sign * F)
            t = SC.tick(w, SUBJ, fx, fy)
            moved = abs(t["dx" if ax == 0 else "dy"])
            deficits[D - moved] += 1
            n += 1
        lines.append("%-5s force %6d raw: starts %d ; deficit D - S (raw units) histogram: %s ; max %d" % (
            direction, F, n, ", ".join("%d:%d" % (k, deficits[k]) for k in sorted(deficits)[:10]), max(deficits)))
out = "\n".join(lines)
print(out)
open(os.path.join(HERE, "..", "out", "studyA2.txt"), "w", encoding="utf-8").write(out + "\n")
