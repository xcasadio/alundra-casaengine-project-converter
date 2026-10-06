"""Independent pixel model of an effect frame under rule B(k) (G3c discovery, 2026-10-06, read only).

Inputs: the G1 export (companion .effects.json + sheet PNG) and a SCENE = list of slots {companion, bank, effect, anim, frame,
raw x/y/z 16.16, depth key inputs}. Nothing of the DLL or of the engine is used: the anchor, the corners, the mirror step back,
the order and the blend are re-derived here from the binary's rules and the plan's rule B(k) (g2b1_predict.bk, exact rationals).

  anchor   = (x >> 16, (y - z) >> 16)             (arithmetic shifts of 32-bit values)
  corners  = anchor + (X1,Y1) (X2,Y2) (X3,Y3) (X4,Y4) - scroll        (PS1 pixels, y down)
  window   = (U - mx, V - my, W, H),  mx = X1 > X2, my = Y1 > Y3      (raw PS1 window; the atlas cell is the SourceX window)
  order    = ascending (elevation, coordinate, localOffset N-1-k, slot) : image 0 of a set last
  blend    = opaque texel: replace; STP texel of a semi quad: ABR0 average, ABR1 add, ABR2 subtract; non-semi quad: replace
"""
import glob
import hashlib
import json
import os
import sys
from fractions import Fraction as F

import numpy as np
from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import g2b1_predict as P  # noqa: E402  (B(k): exact rationals, top-left coverage, floor(u + 1/2))

ROOT = "D:/development/repo/alundra-casaengine-project-converter/alundra-project"


def i32(v):
    v &= 0xFFFFFFFF
    return v - (1 << 32) if v & 0x80000000 else v


def load_companion(map_id):
    if map_id is None:
        path = ROOT + "/Data/effects/effects-global.json"
    else:
        path = [f for f in glob.glob(ROOT + "/Maps/*/*/effects/*.effects.json") if f.endswith("-%d.effects.json" % map_id)][0]
    doc = json.load(open(path, encoding="utf-8"))
    sheets = [n for n in os.listdir(os.path.dirname(path)) if n.endswith("_effectsheet.png")]
    sheet = None
    if sheets:
        sheet = np.array(Image.open(os.path.join(os.path.dirname(path), sheets[0])).convert("RGBA"))
    return doc, sheet


def record_position(rec):
    """SpawnMapEffect: (X*12+12, Y*8+8, Z*8) px in 16.16 (z = Z << 19)."""
    return ((rec["X"] * 12 + 12) << 16, (rec["Y"] * 8 + 8) << 16, rec["Z"] << 19)


def slot_quads(doc, table_idx, anim_idx, frame_idx, pos, slot_index, scroll, doc_sheet_key):
    """The quads one slot pushes, with the engine sort key (elevation, coordinate, localOffset, stableId) and the PS1 corners."""
    table = doc["Effects"][table_idx]
    anim = table["Animations"][anim_idx]
    set_idx = anim["Frames"][frame_idx][1]
    imset = table["ImageSets"][set_idx]
    x, y, z = pos
    anchor = (i32(x) >> 16, i32(i32(y) - i32(z)) >> 16)
    idsv = imset["Idsv"] & 0xFFFF
    key = i32((y & 0xFFFF0000) + (i32(z) >> 16) + (idsv << 16))          # the binary's stored depth key (mode 0)
    row = max(0, min(59, key >> 20))
    elevation = row * 16 + 6
    coordinate = anchor[1] * 100                                          # round(-renderY * 100), renderY = -anchor.y
    n = len(imset["Images"])
    out = []
    for k, im in enumerate(imset["Images"]):
        C = im["C"]
        mx = 1 if C[0] > C[2] else 0
        my = 1 if C[1] > C[5] else 0
        corners = [(anchor[0] + C[0] - scroll[0], anchor[1] + C[1] - scroll[1]),
                   (anchor[0] + C[2] - scroll[0], anchor[1] + C[3] - scroll[1]),
                   (anchor[0] + C[4] - scroll[0], anchor[1] + C[5] - scroll[1]),
                   (anchor[0] + C[6] - scroll[0], anchor[1] + C[7] - scroll[1])]
        out.append(dict(sheet=doc_sheet_key, corners=corners, raw=(im["U"] - mx, im["V"] - my, im["W"], im["H"]),
                        cell=(im["U"], im["V"], im["W"], im["H"]), mirror=(mx, my),
                        mode=(im["Abr"] if im["Semi"] else None),
                        sortkey=(elevation, coordinate, n - 1 - k, slot_index), anchor=anchor, image=k, depthkey=key))
    return out


def blend(dst, src, mode):
    """dst, src: int tuples (r, g, b). mode: None/'opaque' replace, 0 average (alpha 128/255), 1 add, 2 subtract."""
    if mode in (None, "opaque"):
        return src
    if mode == 0:
        a = F(128, 255)
        return tuple(int(round(float(s * a + d * (1 - a)))) for s, d in zip(src, dst))
    if mode == 1:
        return tuple(min(255, s + d) for s, d in zip(src, dst))
    if mode == 2:
        return tuple(max(0, d - s) for s, d in zip(src, dst))
    raise ValueError(mode)


def render(quads, sheets, k, width=320, height=240, bg=(0, 0, 0)):
    """B(k) render of the quads (already sorted by the caller: ascending sort key) over a flat background. Returns int array (h*k, w*k, 3)
    and the number of covered pixels per quad."""
    img = np.zeros((height * k, width * k, 3), dtype=np.int32)
    img[:, :] = bg
    covered = []
    for q in quads:
        sheet = sheets[q["sheet"]]
        sh, sw = sheet.shape[0], sheet.shape[1]
        case = dict(corners=q["corners"], raw=q["raw"], mode=q["mode"])
        tris = P.triangles(case, {})
        xs = [c[0] for c in q["corners"]]
        ys = [c[1] for c in q["corners"]]
        x0, x1 = max(0, k * min(xs) - 1), min(width * k, k * max(xs) + 2)
        y0, y1 = max(0, k * min(ys) - 1), min(height * k, k * max(ys) + 2)
        n_cov = 0
        for sy in range(y0, y1):
            for sx in range(x0, x1):
                hit = None
                for tri, uvs in tris:
                    if P.inside(tri, sx, sy, k):
                        hit = (tri, uvs)
                        break
                if hit is None:
                    continue
                tri, uvs = hit
                p = (F(2 * sx + 1, 2 * k) - F(1, 2), F(2 * sy + 1, 2 * k) - F(1, 2))
                u, v = P.affine(tri, uvs, p)
                tx = min(max(int((u + F(1, 2)).__floor__()), 0), sw - 1)       # PointClamp
                ty = min(max(int((v + F(1, 2)).__floor__()), 0), sh - 1)
                r, g, b, a = (int(c) for c in sheet[ty, tx])
                if a == 0:
                    continue
                n_cov += 1
                if q["mode"] is None or a == 255:
                    img[sy, sx] = (r, g, b)
                else:  # STP texel (alpha 128) of a semi-transparent quad
                    img[sy, sx] = blend(tuple(int(c) for c in img[sy, sx]), (r, g, b), q["mode"])
        covered.append(n_cov)
    return img, covered


def digest(img):
    return hashlib.sha1(img.astype(np.uint8).tobytes()).hexdigest()


def save_png(img, name):
    Image.fromarray(img.astype(np.uint8), "RGB").save(os.path.join(HERE, name))


if __name__ == "__main__":
    pass
