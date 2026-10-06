import sys, numpy as np
from PIL import Image
import redux_nomask as R
import my_render as M
BG = M.BG
def redux_image(quads, sheet):
    img = np.zeros((240, 320, 3), dtype=np.int32); img[:, :] = BG
    sh, sw = sheet.shape[:2]
    for q in quads:
        sx0, sy0, w, h = q["raw"]
        px = R.quad(q["corners"], [(0, 0), (w, 0), (0, h), (w, h)])
        for (x, y), (tu, tv) in px.items():
            if not (0 <= x < 320 and 0 <= y < 240): continue
            r, g, b, a = (int(c) for c in sheet[min(max(sy0 + tv, 0), sh - 1), min(max(sx0 + tu, 0), sw - 1)])
            if a == 0: continue
            if q["mode"] is None or a == 255: img[y, x] = (r, g, b)
            else:
                d = [int(c) for c in img[y, x]]
                if q["mode"] == 0: img[y, x] = [round((128 * s + 127 * dd) / 255) for s, dd in zip((r, g, b), d)]
                elif q["mode"] == 1: img[y, x] = [min(255, s + dd) for s, dd in zip((r, g, b), d)]
                elif q["mode"] == 2: img[y, x] = [max(0, dd - s) for s, dd in zip((r, g, b), d)]
    return img
for name in ("476_aura_tick0", "476_aura_tick2", "476_aura_tick16", "163_rays_tick0", "391_pieces_tick0"):
    doc, sheet, scroll, slots = M.SCENES[name]
    quads = M.quads_of(slots, doc, scroll)
    rimg = redux_image(quads, sheet)
    mine = np.array(Image.open("my_%s_k1.png" % name).convert("RGB")).astype(int)
    theirs = np.array(Image.open("../ref_%s_k1.png" % name).convert("RGB")).astype(int)
    dm = (mine != rimg).any(axis=2); dt = (theirs != rimg).any(axis=2)
    print(name, "mine-vs-R differ", int(dm.sum()), " B(report)-vs-R differ", int(dt.sum()), " mine-vs-B differ", int((mine != theirs).any(axis=2).sum()))
    np.save("R_%s_k1.npy" % name, rimg)
