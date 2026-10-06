"""Ground truth of the aura frame from the REAL code of ALUN_CD.EXE (France): the opcode handlers 0xA2/0x93 (record 0 -> raw (62914560, 10485760, 3145729)),
UpdateEffects tick 0 (its push block fills the element), then the REAL emitter 0x8002DB48 with a camera, compared with the model of g3c_model."""
import sys, os, json
H = os.path.dirname(os.path.abspath(__file__)); sys.path.insert(0, H)
import fxm, g3c_model as M

CAM = (int(sys.argv[1]) if len(sys.argv) > 1 else 800, int(sys.argv[2]) if len(sys.argv) > 2 else -8)
TICKS = int(sys.argv[3]) if len(sys.argv) > 3 else 0
f = fxm.FxMachine(476, hero_tile=(26, 30)); m = f.m
f.map_load_spawn()
f.op(bytes.fromhex("a200c003a0003000")); f.op(bytes.fromhex("9300c003a0003000"))
for t in range(TICKS + 1):
    pushed = f.tick(0)
print("tick", TICKS, "pushed elements", [hex(p) for p in pushed], "slot0", {k: v for k, v in f.slot(0).items() if k in ("x", "y", "z", "depth", "n", "bias", "ta", "ca", "cnt", "dead", "status")})

def get_tpage(tp, abr, x, y):
    return ((tp & 3) << 7) | ((abr & 3) << 5) | ((y & 0x100) >> 4) | ((x & 0x3FF) >> 6) | ((y & 0x200) << 2)
def get_clut(x, y):
    return (y << 6) | ((x >> 4) & 0x3F)
for abr in range(4):
    k = 0
    for y in (0, 0x100):
        for x in range(0x140, 0x400, 0x40):
            m.wh(0x800DC4F0 + abr * 0x2C + 2 * k, get_tpage(0, abr, x, y)); k += 1
PRIMS = 0x80110000; OT = 0x80140000; PTRCELL = 0x80102000
m.ww(0x80126F10, CAM[0]); m.ww(0x80126F14, CAM[1] & 0xFFFFFFFF)
m.ww(PTRCELL, PRIMS)
for i in range(0x40 * 4): m.ww(OT + 4 * i, 0)
n = m.call(0x8002DB48, pushed[0], PTRCELL, OT, 0, limit=50_000_000)
slot_depth = f.slot(0)["depth"] & 0xFFFFFFFF
ot_slot = 16 * min(slot_depth >> 20, 59) + 6
print("emitted", n, "depth", slot_depth, "-> OT slot", ot_slot)
# which OT entries were written (the packets are linked at the head of the slot of the depth key)
touched = [i for i in range(0x40 * 4) if m.rw(OT + 4 * i) != 0]
print("OT entries touched:", touched)
real = []
for i in range(n):
    p = PRIMS + 0x28 * i
    verts = [(m.rhs(p + 0x08), m.rhs(p + 0x0A)), (m.rhs(p + 0x10), m.rhs(p + 0x12)), (m.rhs(p + 0x18), m.rhs(p + 0x1A)), (m.rhs(p + 0x20), m.rhs(p + 0x22))]
    uv = [(m.rb(p + 0x0C), m.rb(p + 0x0D)), (m.rb(p + 0x14), m.rb(p + 0x15)), (m.rb(p + 0x1C), m.rb(p + 0x1D)), (m.rb(p + 0x24), m.rb(p + 0x25))]
    code = m.rb(p + 0x07); tp = m.rh(p + 0x16)
    real.append(dict(verts=verts, uv=uv, semi=(code >> 1) & 1, abr=(tp >> 5) & 3, col=(m.rb(p + 4), m.rb(p + 5), m.rb(p + 6)), code=code))
doc, sheet = M.load_companion(476)
pos = (m.rws(pushed[0] + 4), m.rws(pushed[0] + 8), m.rws(pushed[0] + 0xC))
print("element position (x, y, z) raw:", pos)
# the model uses the frame of the slot's current animation / frame: here the real slot's animation and frame index
sl = f.slot(0)
frame_idx = (sl["fptr"] - sl["f0"]) // 3
print("real slot: current anim", sl["ca"], "frame pointer index (after the update, pointing past the shown frame)", frame_idx)
shown = frame_idx - 1
quads = M.slot_quads(doc, 0, sl["ca"], shown, pos, 0, CAM, "map")
bad = 0
for i, (r, q) in enumerate(zip(real, quads)):
    mv = [tuple(c) for c in q["corners"]]
    ok_v = r["verts"] == mv
    ok_semi = (q["mode"] is not None) == bool(r["semi"]) and (q["mode"] is None or q["mode"] == r["abr"])
    w = r["uv"][1][0] - r["uv"][0][0]; h = r["uv"][2][1] - r["uv"][0][1]
    ok_wh = (w & 0xFF, h & 0xFF) == (q["raw"][2] & 0xFF, q["raw"][3] & 0xFF)
    print(" image", i, "vertices equal", ok_v, "semi/abr equal", ok_semi, "W,H equal", ok_wh, "real colour", r["col"], "code %02x" % r["code"], "" if ok_v else ("real %s model %s" % (r["verts"], mv)))
    bad += (not ok_v) + (not ok_semi) + (not ok_wh)
print("quads real", len(real), "model", len(quads), "mismatches", bad, " model OT slot", (quads[0]["sortkey"][0]))

# walk the OT chain of the slot: head first = drawn first
a = m.rw(OT + 4 * ot_slot) & 0x00FFFFFF
chain = []
for _ in range(64):
    if a == 0: break
    a_full = 0x80000000 | a
    chain.append((a_full - PRIMS) // 0x28 if PRIMS <= a_full < PRIMS + 0x28 * 64 else hex(a_full))
    a = m.rw(a_full) & 0x00FFFFFF
print("OT chain of slot", ot_slot, "(first = drawn first):", chain)
