"""scen_real.py - three real cases on the REAL map cells and prefab data, real physics pass and real Z handlers.

  R260 : map 260 rec17 (I33 broken armour, controller-less, gravity)  C[5]: 63 [128,128,1] / 1B [128,0] / 20 [32,0] / (sound) / 1B [0,0]
  R47  : map 47 rec3 (Sara of the dream, controller-less, gravity)    C[4]: 17 / 64 [128,8,1,192,0,160,0] / 1B [128,255] / 20 [96,0] / 1B [0,0]
  R127 : map 127 rec17 (small iron ball, 304 px)                       C[10]-like loop: 16 / 25 / 17 / 1B [128,0] / 22 / 1B [0,0]

Output: per-tick trace in the binary convention (PosZ raw) and the same minus 1 (DLL convention), plus the tick of every wait result.
"""
import sys, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from zsim import *
sys.stdout.reconfigure(encoding="utf-8")

REC = 0x80420000


def record_xy(r):
    return (int(r["XPos"]) * 12 + 12) << 16, (int(r["YPos"]) * 8 + 8) << 16


def build(map_id, rec_idx, extra=None):
    s = Sim(map_id=map_id)
    r = [x for x in s.mp.records if int(x["Index"]) == rec_idx][0]
    h = SPR[r["PrefabAssetId"]]
    fl = h["MoreFlags"] | (h["CanPickup"] << 8) | (h["FlagsPortraitShadowType"] << 16)
    x, y = record_xy(r)
    z = (int(r["Height"]) << 19) - (h["OffsetZ"] << 16) + 1          # InitializeEntity 0x80039EA0: z - ModZ + 1
    idx = 1
    s.add(idx, fl, off=(h["OffsetX"], h["OffsetY"], h["OffsetZ"]), size=(h["SizeX"], h["SizeY"], h["SizeZ"]), pos=(x, y, z))
    s.init_state(idx)                                                 # T probe, PosZ <= T+1 -> T+1, end pass
    for i in range(0x20): s.mem.wb(REC + i, 0)
    s.mem.wb(REC + 9, int(r["Height"])); s.mem.ww(ent(idx) + F["record"], REC)
    return s, idx, r, h


def row(s, idx, t, tr, note=""):
    q = s.snap(idx)
    ops = " ".join("%02x:%s" % (b, "-" if c is None else c) for a, b, c in tr.get(idx, [])) if tr else ""
    print("%4d | %10d %10d | %9d %9d | %9d | %d %d | %s %s" % (t, q["posz"], q["posz"] - 1, q["forcez"], q["finalz"], q["terrain"], q["ground"], q["coll"], ops, note))


HDR = "   t |  PosZ(bin)  PosZ-1(dll) |    ForceZ FinalForce |  Terrain | Gnd Coll | script ops (opcode:result)"


def r260():
    s, idx, r, h = build(260, 17)
    print("== R260: map 260 rec17, flags %#x, terrain %d, spawn PosZ %d (rest = T+1)" % (s.get(idx, "flags") & M32, s.get(idx, "terrain"), s.get(idx, "posz")))
    print(HDR)
    for t in range(1, 4):
        tr = s.tick(); row(s, idx, -4 + t, tr, "(rest, before C[5])")
    code = bytes([0x63, 128, 128, 1, 0x1B, 128, 0, 0x20, 32, 0, 0xBD, 53, 0, 0x1B, 0, 0, 0xFF])
    s.load_program(idx, code)
    waits = []
    for t in range(0, 70):
        tr = s.tick(scripts=[idx] if t < 67 else [])
        done = any(b == 0x20 and c == 3 for a, b, c in tr.get(idx, []))
        if t < 3 or t >= 62 or done:
            row(s, idx, t, tr, "<<< 0x20 returned 3" if done else "")
        if done:
            waits.append(t)
            break
    print("  0x20 [32,0] returned 3 at script call #%d after the first (first call at t=0)" % waits[0])
    q = s.snap(idx)
    # after the wait: the tail of the program ran (sound, 1B [0,0], FF); two more ticks of physics
    for t in range(waits[0] + 1, waits[0] + 3):
        tr = s.tick(scripts=[]); row(s, idx, t, tr, "(after)")


def r47():
    s, idx, r, h = build(47, 3)
    print("== R47: map 47 rec3, flags %#x, terrain %d, spawn PosZ %d" % (s.get(idx, "flags") & M32, s.get(idx, "terrain"), s.get(idx, "posz")))
    print(HDR)
    for t in range(1, 4):
        tr = s.tick(); row(s, idx, -4 + t, tr, "(rest, before C[4])")
    code = bytes([0x17, 0x64, 128, 8, 1, 192, 0, 160, 0, 0x1B, 128, 255, 0x20, 96, 0, 0x1B, 0, 0, 0xFF])
    s.load_program(idx, code)
    for t in range(0, 200):
        tr = s.tick(scripts=[idx] if t < 195 else [])
        done = any(b == 0x20 and c == 3 for a, b, c in tr.get(idx, []))
        if t < 3 or t >= 189 or done:
            row(s, idx, t, tr, "<<< 0x20 returned 3" if done else "")
        if done:
            print("  0x20 [96,0] returned 3 at t=%d (first call at t=0)" % t)
            break
    for t in range(done and (t + 1), t + 3):
        tr = s.tick(scripts=[]); row(s, idx, t, tr, "(after)")


def r127(n_cycles=1):
    s, idx, r, h = build(127, 17)
    print("== R127: map 127 rec17, flags %#x, size %s, terrain(probe) %d (= %d px), spawn PosZ %d (target %d)" % (
        s.get(idx, "flags") & M32, (h["SizeX"], h["SizeY"], h["SizeZ"]), s.get(idx, "terrain"), s.get(idx, "terrain") // U, s.get(idx, "posz"), int(r["Height"]) << 19))
    print(HDR)
    code = bytes([0x16, 0x25, 0x17, 0x1B, 128, 0, 0x22, 0x1B, 0, 0, 0x00, 0xFF])
    s.load_program(idx, code)
    log = []
    last = None
    for t in range(0, 900):
        tr = s.tick(scripts=[idx])
        ops = tr.get(idx, [])
        res = [(b, c) for a, b, c in ops]
        q = s.snap(idx)
        interesting = [x for x in ops if x[1] in (0x25, 0x22, 0x1B, 0x16, 0x17) and x[2] not in (0, None) or x[1] in (0x16, 0x17, 0x1B)]
        if t < 3 or interesting or (q["posz"] != (last or 0) and False):
            row(s, idx, t, tr, "")
        last = q["posz"]
        if any(b == 0x22 and c == 1 for a, b, c in ops):
            print("  0x22 returned 1 at t=%d, PosZ=%d target=%d" % (t, q["posz"], int(r["Height"]) << 19))
            break
    return s, idx


if __name__ == "__main__":
    which = sys.argv[1] if len(sys.argv) > 1 else "all"
    if which in ("260", "all"): r260()
    if which in ("47", "all"): r47()
    if which in ("127", "all"): r127()
