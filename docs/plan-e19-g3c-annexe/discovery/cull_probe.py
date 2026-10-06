"""Boundaries of the anchor cull of the REAL emitter 0x8002DB48: which anchors (screen px = (x>>16) - camX, ((y-z)>>16) - camY) emit."""
import sys, os
H = os.path.dirname(os.path.abspath(__file__)); sys.path.insert(0, H)
import fxm
f = fxm.FxMachine(476, hero_tile=(26, 30)); m = f.m
f.map_load_spawn(); f.op(bytes.fromhex("a200c003a0003000")); f.op(bytes.fromhex("9300c003a0003000"))
pushed = f.tick(0)
PRIMS = 0x80110000; OT = 0x80140000; PTRCELL = 0x80102000
def emit_at(ax, ay, camx=0, camy=0):
    el = pushed[0]
    m.ww(0x80126F10, camx & 0xFFFFFFFF); m.ww(0x80126F14, camy & 0xFFFFFFFF)
    m.ww(el + 4, (ax + camx) << 16); m.ww(el + 8, (ay + camy) << 16); m.ww(el + 0xC, 0)
    m.ww(PTRCELL, PRIMS)
    for i in range(0x40 * 4): m.ww(OT + 4 * i, 0)
    return m.call(0x8002DB48, el, PTRCELL, OT, 0, limit=50_000_000)
print("x scan (y=120):", {ax: emit_at(ax, 120) for ax in (-130, -129, -128, -127, 447, 448, 449, 450)})
print("y scan (x=160):", {ay: emit_at(160, ay) for ay in (-130, -129, -128, -127, 366, 367, 368, 369, 370)})
print("with camera (800,-8): x", {ax: emit_at(ax, 120, 800, -8) for ax in (-129, -128, 448, 449)}, "y", {ay: emit_at(160, ay, 800, -8) for ay in (-129, -128, 368, 369)})
# the +1 on z: y - z = (112<<16) - 1 -> row 111 ; y - z = 112<<16 exactly -> 112
el = pushed[0]
for z in (48 << 16, (48 << 16) + 1, (48 << 16) - 1):
    m.ww(el + 4, 160 << 16); m.ww(el + 8, 168 << 16); m.ww(el + 0xC, z & 0xFFFFFFFF); m.ww(0x80126F10, 0); m.ww(0x80126F14, 0)
    m.ww(PTRCELL, PRIMS)
    for i in range(0x40 * 4): m.ww(OT + 4 * i, 0)
    n = m.call(0x8002DB48, el, PTRCELL, OT, 0, limit=50_000_000)
    print("z raw", z, "-> first vertex y", m.rhs(PRIMS + 0x0A), "(model: anchor y", ((168 << 16) - z) >> 16, "+ C[1] -112 = ", (((168 << 16) - z) >> 16) - 112, ")")
