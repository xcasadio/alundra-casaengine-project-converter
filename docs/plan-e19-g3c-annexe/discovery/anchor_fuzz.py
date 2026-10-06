"""Random sweep of the anchor/cull/camera arithmetic of the REAL emitter 0x8002DB48 against the model rule
   screen = ((x >> 16) - camX, ((y - z) >> 16) - camY) [int32, arithmetic shifts], emitted iff -128 <= screen.x <= 448 and -128 <= screen.y <= 368."""
import sys, os, random
H = os.path.dirname(os.path.abspath(__file__)); sys.path.insert(0, H)
import fxm
def i32(v):
    v &= 0xFFFFFFFF
    return v - (1 << 32) if v & 0x80000000 else v
f = fxm.FxMachine(476, hero_tile=(26, 30)); m = f.m
f.map_load_spawn(); f.op(bytes.fromhex("a200c003a0003000")); f.op(bytes.fromhex("9300c003a0003000"))
pushed = f.tick(0)
el = pushed[0]
PRIMS = 0x80110000; OT = 0x80140000; PTRCELL = 0x80102000
base = []
# the vertices of image 0 relative to the anchor, read once at the origin
def run(x, y, z, cx, cy):
    m.ww(0x80126F10, cx & 0xFFFFFFFF); m.ww(0x80126F14, cy & 0xFFFFFFFF)
    m.ww(el + 4, x & 0xFFFFFFFF); m.ww(el + 8, y & 0xFFFFFFFF); m.ww(el + 0xC, z & 0xFFFFFFFF)
    m.ww(PTRCELL, PRIMS)
    for i in range(0x40 * 4): m.ww(OT + 4 * i, 0)
    n = m.call(0x8002DB48, el, PTRCELL, OT, 0, limit=50_000_000)
    return n, (m.rhs(PRIMS + 0x08), m.rhs(PRIMS + 0x0A)) if n else None
n0, v0 = run(0, 0, 0, 0, 0)
rel = v0  # vertex 0 of image 0 relative to anchor (0,0) screen, i.e. its C[0], C[1]
print("relative vertex 0 of image 0:", rel)
random.seed(7)
bad = 0; emitted = 0; total = 0
for _ in range(3000):
    x = random.randint(-(300 << 16), 700 << 16); y = random.randint(-(300 << 16), 700 << 16); z = random.choice([0, random.randint(-(100 << 16), 100 << 16), (random.randint(0, 64) << 16) + random.choice((0, 1, -1))])
    cx = random.randint(-100, 900); cy = random.randint(-100, 700)
    ax = (x >> 16) - cx; ay = (i32(y - z) >> 16) - cy
    expect_emit = (-128 <= ax <= 448) and (-128 <= ay <= 368)
    n, v = run(x, y, z, cx, cy)
    total += 1
    if (n > 0) != expect_emit:
        bad += 1; print("cull mismatch", (x, y, z, cx, cy), n, ax, ay)
    elif n > 0:
        emitted += 1
        if v != (ax + rel[0], ay + rel[1]):
            bad += 1; print("vertex mismatch", (x, y, z, cx, cy), v, (ax + rel[0], ay + rel[1]))
print("random cases", total, "emitted", emitted, "mismatches", bad)
