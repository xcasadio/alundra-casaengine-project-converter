"""Proposed real-data scenes that exercise the mirror step-back (the three scenes of the report contain no mirrored quad)."""
import json, sys, time
import numpy as np
from PIL import Image
import my_render as M

def mirrored(q):
    c = q["corners"]; return c[0][0] > c[1][0] or c[0][1] > c[2][1]

def scene(name, mid, eff, anim, frame, pos, scroll, ks=(1, 3)):
    doc, sheet = M.load(mid)
    qs = M.quads_of([(eff, anim, frame, pos, 0)], doc, scroll)
    info = dict(map=mid, effect=eff, anim=anim, frame=frame, quads=len(qs), mirrored=sum(mirrored(q) for q in qs),
                origin_below_0=sum(1 for q in qs if q["raw"][0] < 0 or q["raw"][1] < 0), modes=sorted({str(q["mode"]) for q in qs}),
                scaled=sum(1 for q in qs if abs(q["corners"][1][0] - q["corners"][0][0]) != q["raw"][2] or abs(q["corners"][2][1] - q["corners"][0][1]) != q["raw"][3]))
    for k in ks:
        t0 = time.time(); img = M.render(qs, sheet, k)
        info["k%d" % k] = dict(lit=int((img != np.array(M.BG)).any(axis=2).sum()), sha1=M.sha(img))
    print(name, json.dumps(info))

# 476 effect 0 animation 3 (Loop), frame 2 (set 90, three mirrored images), anchor (160,119) like the aura (raw 0xA2 position)
scene("476_anim3_frame2", 476, 0, 3, 2, (960 << 16, 160 << 16, (48 << 16) + 1), (800, -8))
scene("476_anim3_frame10", 476, 0, 3, 10, (960 << 16, 160 << 16, (48 << 16) + 1), (800, -8))
# find a mirrored-at-origin-0 frame in map 327 effect 0
doc, sheet = M.load(327)
e = doc["Effects"][0]
best = None
for ai, a in enumerate(e["Animations"]):
    for fi, (d, si) in enumerate(a["Frames"]):
        s = e["ImageSets"][si]
        n0 = sum(1 for im in s["Images"] if (im["C"][0] > im["C"][2] and im["U"] == 0) or (im["C"][1] > im["C"][5] and im["V"] == 0))
        if n0 and (best is None or len(s["Images"]) < best[3]):
            best = (ai, fi, si, len(s["Images"]), n0)
print("map 327 effect 0: smallest frame with a mirrored image at window origin -1:", best)
if best:
    scene("327_origin0", 327, 0, best[0], best[1], (960 << 16, 160 << 16, (48 << 16) + 1), (800, -8))
