import sys
from hh import *
sys.stdout.reconfigure(encoding="utf-8")

def force_branch(h, addr):
    w = h.e.rw(addr)
    nw = (4 << 26) | (w & 0xFFFF)     # beq $0,$0,<same offset>: always taken
    h.e.ww(addr, nw)

def run(patch):
    h = H()
    X, Y = 100 << 16, 100 << 16
    zp = 1 + 40 * U
    h.add(1, PLAT_FLAGS, PLAT_OFF, PLAT_SIZE, (X, Y, zp))
    h.add(0, HERO_FLAGS, HERO_OFF, HERO_SIZE, (X, Y, zp + 8 * U))
    if patch:
        force_branch(h, 0x800376c8)     # landing: "target already moved?" always true -> never move the target first
    out = []
    for t in range(10):
        if t == 2: h.p(1, "forcez", -65536)
        if t == 7: h.p(1, "forcez", 0)
        h.tick()
        a, c = h.snap(0), h.snap(1)
        out.append("t%02d plat z=%9d | hero z=%9d rel=%8d coll=%d gnd=%d" % (t, c["z"], a["z"], a["z"]-c["z"]-8*U, a["coll"], a["ground"]))
    return out
print("--- real code"); print("\n".join(run(False)))
print("--- mutant: no 'move the target first' on a landing (0x800376C8 forced)"); print("\n".join(run(True)))
