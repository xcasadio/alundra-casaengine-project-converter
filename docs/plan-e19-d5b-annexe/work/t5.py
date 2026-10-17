import sys
sys.path.insert(0, ".")
from oracle_p import *
sys.stdout.reconfigure(encoding="utf-8")
CH = dict(flags=PLAT_FLAGS_SMALL, off=(-12, -8, 0), size=(24, 16, 16))   # 24 x 16 x 16 block (chest-like carrier)
def run(title, hero_x_px, script, n=9):
    p = P(cells=lambda c, r: (0 if (c == 4 and r == 6) else 1, 0, 0, 0))     # terrain 16 px everywhere, a hollow of 0 px in the cell (4, 6) = x 96..120, y 96..112
    p.add(1, CH["flags"], CH["off"], CH["size"], (108 << 16, 104 << 16, 1))   # block in the hollow: top + 1 = 16 px + 1 = flush with the terrain
    p.add(0, HERO_FLAGS, HERO_OFF, HERO_SIZE, (hero_x_px << 16, 104 << 16, (1 << 20) + 1))
    print("==", title)
    for t in range(n):
        script(p, t)
        p.tick()
        h, c = p.snap(0), p.snap(1)
        print("t%d block z=%8d | hero z=%8d (rel %7d) terrain-under-hero=%8d ride=%s coll=%d gnd=%d" % (t, c["z"], h["z"], h["z"] - c["z"] - 16 * U, h["terr"], h["ride"], h["coll"], h["ground"]))
def rise(p, t):
    if t == 2: p.put(1, "forcez", 65536)
    if t == 6: p.put(1, "forcez", 0)
def descend_from_top(p, t):
    if t == 2: p.put(1, "forcez", -65536)
run("hero half on the dock (x=120): block rises 1 px/tick t2..t5", 120, rise)
run("hero fully over the hollow (x=108): block rises", 108, rise)

def run2(title, hero_x_px, n=8):
    # the block starts 8 px ABOVE the dock (top 24 px), hero on it (z = 24 px + 1), terrain under the hero 16 px if half on the dock, 0 if over the hollow; block descends 1 px/tick
    p = P(cells=lambda c, r: (0 if (c == 4 and r == 6) else 1, 0, 0, 0))
    p.add(1, CH["flags"], CH["off"], CH["size"], (108 << 16, 104 << 16, 1 + 8 * U))
    p.add(0, HERO_FLAGS, HERO_OFF, HERO_SIZE, (hero_x_px << 16, 104 << 16, 1 + 8 * U + 16 * U))
    print("==", title)
    for t in range(n):
        if t == 2: p.put(1, "forcez", -65536)
        p.tick()
        h, c = p.snap(0), p.snap(1)
        print("t%d block z=%8d | hero z=%8d (rel %7d) terrain-under-hero=%8d ride=%s coll=%d gnd=%d" % (t, c["z"], h["z"], h["z"] - c["z"] - 16 * U, h["terr"], h["ride"], h["coll"], h["ground"]))
run2("block descends 1 px/tick from 8 px above the dock, hero half on the dock side (x=120)", 120, 14)
run2("same, hero fully over the hollow (x=108)", 108)
