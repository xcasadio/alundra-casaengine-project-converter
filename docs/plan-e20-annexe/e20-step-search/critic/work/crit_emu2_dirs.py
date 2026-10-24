import sys, os, collections, random
HERE = os.path.dirname(os.path.abspath(__file__)); sys.path.insert(0, HERE)
import crit_emu2 as C
U = C.U
e = C.make(); rnd = random.Random(7)
def run(name, setup, d, axis, fmax, sign, F, n=60):
    setup(e); dh = collections.Counter()
    for _ in range(n):
        D = rnd.randint(1, F-1)
        pos = [300*U, 300*U, 1]; pos[axis] = fmax - sign*D
        C.put(e, C.SUBJ, 0, pos, d=d); C.set_collidables(e, [])
        t = C.tick(e, C.SUBJ, *( (sign*F, 0) if axis == 0 else (0, sign*F)))
        moved = abs(t["x"] - pos[0]) if axis == 0 else abs(e.rws(C.SUBJ+0x118) - pos[1])
        dh[D - moved] += 1
    print(name, "F=%d"%F, dict(sorted(dh.items())))
def west(e):
    C.wipe_cells(e)
    for c in range(0,5):
        for r in range(60): C.cell(e,c,r,0x40)
def south(e):
    C.wipe_cells(e)
    for c in range(52):
        for r in range(10,60): C.cell(e,c,r,0x40)
for F in (159744, 196608):
    run("west", west, 8, 0, 130*U, -1, F)
    run("south", south, 0, 1, 152*U, 1, F)
