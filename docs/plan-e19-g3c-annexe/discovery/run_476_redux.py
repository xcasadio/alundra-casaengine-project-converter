import time, numpy as np
import g3c_model as M, redux_nomask as R
doc, sheet = M.load_companion(476)
sheets = {"map": sheet}
pos = (960 << 16, 160 << 16, (48 << 16) + 1)
scroll = (800, -8)

def render_r(quads, k=1, width=320, height=240):
    img = np.zeros((height, width, 3), dtype=np.int32)
    sh, sw = sheet.shape[0], sheet.shape[1]
    for q in quads:
        sx0, sy0, w, h = q["raw"]
        uvs = [(0, 0), (w, 0), (0, h), (w, h)]
        px = R.quad(q["corners"], uvs)
        for (x, y), (tu, tv) in px.items():
            if not (0 <= x < width and 0 <= y < height):
                continue
            tx = min(max(sx0 + tu, 0), sw - 1); ty = min(max(sy0 + tv, 0), sh - 1)
            r, g, b, a = (int(c) for c in sheet[ty, tx])
            if a == 0: continue
            if q["mode"] is None or a == 255: img[y, x] = (r, g, b)
            else: img[y, x] = M.blend(tuple(int(c) for c in img[y, x]), (r, g, b), q["mode"])
    return img

for frame in (0, 1, 8):
    quads = M.slot_quads(doc, 0, 1, frame, pos, 0, scroll, "map"); quads.sort(key=lambda q: q["sortkey"])
    b, cov = M.render(quads, sheets, 1)
    r = render_r(quads)
    diff = (b != r).any(axis=2)
    nz = ((b.sum(axis=2) > 0) | (r.sum(axis=2) > 0)).sum()
    print("frame", frame, "B vs R pixels differing:", int(diff.sum()), "of union nonblack", int(nz), "= %.2f%%" % (100.0 * diff.sum() / nz))
