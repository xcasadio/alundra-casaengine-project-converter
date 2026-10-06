import time, json, sys
import g3c_model as M
doc, sheet = M.load_companion(476)
sheets = {"map": sheet}
# the real opcode: 0xA2 [0, 960, 160, 48] -> raw (62914560, 10485760, 3145729), record 0 -> anim 1
pos = (960 << 16, 160 << 16, (48 << 16) + 1)
scroll = (800, -8)                       # anchor (960, 111) - scroll -> screen (160, 119)
for frame in (0, 1, 8):
    quads = M.slot_quads(doc, 0, 1, frame, pos, 0, scroll, "map")
    quads.sort(key=lambda q: q["sortkey"])
    t = time.time()
    img, cov = M.render(quads, sheets, 1)
    nz = int((img.sum(axis=2) > 0).sum())
    print("frame", frame, "anchor", quads[0]["anchor"], "depthkey", quads[0]["depthkey"], "elev", quads[0]["sortkey"][0],
          "covered per quad", cov, "nonblack", nz, "sha1", M.digest(img)[:16], "%.1fs" % (time.time() - t))
    M.save_png(img, "ref_476_a1_f%d_k1.png" % frame)
    for (x, y) in ((160, 119), (160, 120), (150, 100), (170, 140), (200, 150), (120, 90), (160, 60)):
        print("   probe", (x, y), tuple(int(c) for c in img[y, x]))
