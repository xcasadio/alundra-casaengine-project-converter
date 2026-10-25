"""gen_named.py - E20.e0k5 T1: the named cases of the halving search, made from the hand model of the binary (xymodel.py, see gen_vectors.py).
The world is the wall of the point-blank reading: a flat field, ONE wall column (column 5, x >= 120, attribute 0x40), a projectile-like subject (non-hero, NoObstacleSlide
0x2000, no ground snapping, no collidable) with a box of 11 pixels from its position (the box right edge is pos + 11 pixels - 1 raw unit, so the wall is touched
when pos > 109 pixels = 7143424 raw: the 'x = 109' of the hunter arrow). Y is 'north-south' rows: a second block of cases uses a wall row (row 8, y >= 128).
Usage: python -I -B gen_named.py <out.tsv>
Columns: name \t cardinal \t forceX \t forceY \t startX(16.16, absolute) \t startY \t resultDx \t resultDy \t accepted \t queries
"""
import sys, os

HERE = os.path.dirname(os.path.abspath(__file__))
WORK = os.path.normpath(os.path.join(HERE, "..", "..", "e20-step-search", "work"))
sys.path.insert(0, WORK)
import xymodel as XM   # noqa: E402

A = 0x80127D30 + 0x294


def world(wall):
    w = XM.World()
    w.tile_base = 0x80310000
    for x in range(0, 1248):
        w.xtab[x] = min(x // 24, 51)
    for c in range(0, 52):
        for r in range(0, 60):
            w.cell[(c, r)] = (0, 0, 0)
    if wall == "col5":
        for r in range(0, 60):
            w.cell[(5, r)] = (0x40, 0, 0)
    elif wall == "row8":
        for c in range(0, 52):
            w.cell[(c, 8)] = (0x40, 0, 0)
    w.gravflag = 3
    E = {}
    w.ents[A] = E
    E[0x6c] = 0x2000
    E[0x1e4] = E[0x1e8] = E[0x1ec] = 0
    E[0x1f0] = (11 << 16) - 1
    E[0x1f4] = (11 << 16) - 1
    E[0x1f8] = 0
    return w


def run(name, wall, direction, fx, fy, x0, y0):
    w = world(wall)
    E = w.ents[A]
    E[0x8c] = direction
    E[0xe4], E[0xe8] = fx, fy
    E[0x114], E[0x118], E[0x11c] = x0, y0, 1
    XM.set_mod(E)
    trace = []
    XM.compute_xy(w, A, trace)
    q = []
    acc = 0
    for rec in trace:
        px, py, _ = rec["pos"]
        q.append(f"{XM.s32(px - x0)},{XM.s32(py - y0)},{1 if rec['blocked'] else 0}")
        acc |= 0 if rec["blocked"] else 1
    cardinal = 1 if direction & 7 == 0 else 0
    return "\t".join([name, str(cardinal), str(fx), str(fy), str(x0), str(y0), str(XM.s32(E[0x114] - x0)), str(XM.s32(E[0x118] - y0)), str(acc), ";".join(q)])


def main():
    rows = []
    Y0 = 40 << 16
    # the 10 cells of the table of the reading (3 px: arrows; 6 px: the charged arrow), east
    for step, xs in ((3, (105, 106, 107, 108, 109)), (6, (104, 105, 107, 108, 109))):
        for x in xs:
            rows.append(run(f"east_{step}px_x{x}", "col5", 24, step << 16, 0, x << 16, Y0))
    # free step, flush, zero vector, a unit of fraction, the sra of -1
    rows.append(run("east_free_3px_x100", "col5", 24, 3 << 16, 0, 100 << 16, Y0))
    rows.append(run("east_zero", "col5", 24, 0, 0, 108 << 16, Y0))
    rows.append(run("east_1unit_flush", "col5", 24, 1, 0, 7143424, Y0))
    rows.append(run("east_1unit_free", "col5", 24, 1, 0, 7143423, Y0))
    rows.append(run("east_minus1_free", "col5", 8, -1, 0, 7143424, Y0))
    rows.append(run("east_3unit_halves", "col5", 24, 3, 0, 7143423, Y0))
    # west (the wall column 5 is behind: free), north, south against the wall row 8
    rows.append(run("west_3px_free", "col5", 8, -(3 << 16), 0, 109 << 16, Y0))
    for y in (114, 115, 116, 117):
        rows.append(run(f"south_2px_y{y}", "row8", 0, 0, 2 << 16, 40 << 16, y << 16))
    # a diagonal against the wall: both axes at once, the loop ends when either axis is 0 (non cardinal)
    rows.append(run("diag_se_x107", "col5", 4, 4 << 16, 4 << 16, 107 << 16, Y0))
    rows.append(run("diag_ne_x109", "col5", 28, 5 << 16, -(5 << 16), 105 << 16, Y0))
    open(sys.argv[1], "w", newline="\n").write("\n".join(rows) + "\n")
    for r in rows:
        print(r)


if __name__ == "__main__":
    main()
