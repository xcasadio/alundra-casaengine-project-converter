"""My own independent re-implementation of the G3c reference frames (rule B(k) of the plan), written from the plan text and the binary facts,
sharing NO code with the discovery scripts (g3c_model.py, g2b1_predict.py). Inputs: the export JSON and the sheet PNG only.

Conventions (from the plan, section 'Regle de texel corrigee' and 'Regle a la resolution de l'ecran'):
  coverage : the top-left corner of the screen pixel, (sx/k, sy/k) in PS1 units, against the quad, top-left rule (two triangles TL-TR-BL and TR-BL-BR)
  texel    : floor(u(p) + 1/2), p = ((sx+1/2)/k - 1/2, (sy+1/2)/k - 1/2), u = affine map of the covering triangle
  window   : raw = (U - mx, V - my, W, H), mx = X1 > X2, my = Y1 > Y3 ; vertex UVs (u,v) (u+w,v) (u,v+h) (u+w,v+h) for TL TR BL BR
  blend    : opaque texel (alpha 255) replaces ; STP texel (alpha 128) of a semi quad: ABR0 (128 s + 127 d)/255, ABR1 d+s, ABR2 d-s ; non-semi quad: replace
"""
import glob, hashlib, json, os, sys, time
from fractions import Fraction as F
import numpy as np
from PIL import Image

ROOT = "D:/development/repo/alundra-casaengine-project-converter/alundra-project"
HERE = os.path.dirname(os.path.abspath(__file__))
BG = (24, 40, 56)

def load(mid):
    p = [f for f in glob.glob(ROOT + "/Maps/*/*/effects/*.effects.json") if f.endswith("-%d.effects.json" % mid)][0]
    d = json.load(open(p, encoding="utf-8"))
    sheet = [f for f in os.listdir(os.path.dirname(p)) if f.endswith("_effectsheet.png")][0]
    return d, np.array(Image.open(os.path.join(os.path.dirname(p), sheet)).convert("RGBA"))

def sign(v): return (v > 0) - (v < 0)

def inside(tri, p):
    """tri: three (x, y) vertices (y down). p: (x, y) rational. Top-left rule, orientation independent."""
    for i in range(3):
        a, b, c = tri[i], tri[(i + 1) % 3], tri[(i + 2) % 3]
        cr = lambda q: (b[0] - a[0]) * (q[1] - a[1]) - (b[1] - a[1]) * (q[0] - a[0])
        s = sign(cr(c))
        if s == 0:
            return False                                   # degenerate triangle
        v = cr(p) * s
        if v < 0:
            return False
        if v == 0:
            # on the edge line: included only if it is a top edge (horizontal, interior below) or a left edge (interior to the right)
            if b[1] == a[1]:
                ok = c[1] > a[1]
            else:
                xe = a[0] + F(c[1] - a[1]) * (b[0] - a[0]) / (b[1] - a[1])
                ok = c[0] > xe
            if not ok:
                return False
    return True

def affine(tri, uv, p):
    (x0, y0), (x1, y1), (x2, y2) = tri
    det = F((x1 - x0) * (y2 - y0) - (x2 - x0) * (y1 - y0))
    l1 = ((p[0] - x0) * (y2 - y0) - (x2 - x0) * (p[1] - y0)) / det
    l2 = ((x1 - x0) * (p[1] - y0) - (p[0] - x0) * (y1 - y0)) / det
    l0 = 1 - l1 - l2
    return (l0 * uv[0][0] + l1 * uv[1][0] + l2 * uv[2][0], l0 * uv[0][1] + l1 * uv[1][1] + l2 * uv[2][1])

def quads_of(slots, doc, scroll):
    """slots: list of (table, anim, frame, (x, y, z) raw 16.16, slot index) -> sorted quads"""
    out = []
    for (t, a, f, (x, y, z), si) in slots:
        table = doc["Effects"][t]
        imset = table["ImageSets"][table["Animations"][a]["Frames"][f][1]]
        ax = x >> 16; ay = (y - z) >> 16
        if not (-128 <= ax - scroll[0] <= 448 and -128 <= ay - scroll[1] <= 368):
            continue
        key = (y & ~0xFFFF) + (z >> 16) + ((imset["Idsv"] & 0xFFFF) << 16)
        row = max(0, min(59, key >> 20))
        n = len(imset["Images"])
        for k, im in enumerate(imset["Images"]):
            C = im["C"]
            mx = int(C[0] > C[2]); my = int(C[1] > C[5])
            corners = [(ax + C[2 * i] - scroll[0], ay + C[2 * i + 1] - scroll[1]) for i in range(4)]   # TL TR BL BR
            out.append(dict(sort=(16 * row + 6, 100 * ay, n - 1 - k, si), corners=corners, raw=(im["U"] - mx, im["V"] - my, im["W"], im["H"]),
                            mode=(im["Abr"] if im["Semi"] else None), image=k, slot=si))
    out.sort(key=lambda q: q["sort"])
    return out

def render(quads, sheet, k, bg=BG):
    H, W = 240 * k, 320 * k
    img = np.empty((H, W, 3), dtype=np.int32); img[:, :] = bg
    sh, sw = sheet.shape[:2]
    for q in quads:
        (tl, tr, bl, br) = q["corners"]
        u0, v0, w, h = q["raw"]
        uvs = {tl: (u0, v0), tr: (u0 + w, v0), bl: (u0, v0 + h), br: (u0 + w, v0 + h)}
        tris = [((tl, tr, bl), [(u0, v0), (u0 + w, v0), (u0, v0 + h)]), ((tr, bl, br), [(u0 + w, v0), (u0, v0 + h), (u0 + w, v0 + h)])]
        xs = [c[0] for c in q["corners"]]; ys = [c[1] for c in q["corners"]]
        for sy in range(max(0, k * min(ys) - 1), min(H, k * max(ys) + 2)):
            for sx in range(max(0, k * min(xs) - 1), min(W, k * max(xs) + 2)):
                cp = (F(sx, k), F(sy, k))
                hit = None
                for tri, uv in tris:
                    if inside(tri, cp):
                        hit = (tri, uv); break
                if hit is None:
                    continue
                p = (F(2 * sx + 1, 2 * k) - F(1, 2), F(2 * sy + 1, 2 * k) - F(1, 2))
                u, v = affine(hit[0], hit[1], p)
                tx = min(max((u + F(1, 2)).__floor__(), 0), sw - 1); ty = min(max((v + F(1, 2)).__floor__(), 0), sh - 1)
                r, g, b, a = (int(c) for c in sheet[ty, tx])
                if a == 0:
                    continue
                if q["mode"] is None or a == 255:
                    img[sy, sx] = (r, g, b)
                else:
                    d = [int(c) for c in img[sy, sx]]
                    if q["mode"] == 0: img[sy, sx] = [round(F(128 * s + 127 * dd, 255)) for s, dd in zip((r, g, b), d)]
                    elif q["mode"] == 1: img[sy, sx] = [min(255, s + dd) for s, dd in zip((r, g, b), d)]
                    elif q["mode"] == 2: img[sy, sx] = [max(0, dd - s) for s, dd in zip((r, g, b), d)]
                    else: raise ValueError(q["mode"])
    return img

def sha(img): return hashlib.sha1(img.astype(np.uint8).tobytes()).hexdigest()

def pos_of(r): return ((r["X"] * 12 + 12) << 16, (r["Y"] * 8 + 8) << 16, r["Z"] << 19)

SCENES = {}
d476, s476 = load(476)
for t, frame in ((0, 0), (2, 1), (16, 8)):
    SCENES["476_aura_tick%d" % t] = (d476, s476, (800, -8), [(0, 1, frame, (960 << 16, 160 << 16, (48 << 16) + 1), 0)])
d163, s163 = load(163)
SCENES["163_rays_tick0"] = (d163, s163, (220, 40), [(r["Effect"], r["Anim"], 0, pos_of(r), i) for i, r in enumerate(d163["Records"])])
d391, s391 = load(391)
SCENES["391_pieces_tick0"] = (d391, s391, (836, 640), [(r["Effect"], r["Anim"], 0, pos_of(r), i) for i, r in enumerate(d391["Records"])])

if __name__ == "__main__":
    names = sys.argv[1].split(",") if len(sys.argv) > 1 else list(SCENES)
    ks = [int(a) for a in sys.argv[2].split(",")] if len(sys.argv) > 2 else [1]
    out = {}
    for name in names:
        doc, sheet, scroll, slots = SCENES[name]
        quads = quads_of(slots, doc, scroll)
        for k in ks:
            t0 = time.time()
            img = render(quads, sheet, k)
            lit = int((img != np.array(BG)).any(axis=2).sum())
            Image.fromarray(img.astype(np.uint8), "RGB").save(os.path.join(HERE, "my_%s_k%d.png" % (name, k)))
            out["%s_k%d" % (name, k)] = dict(quads=len(quads), lit=lit, sha1=sha(img))
            print(name, "k", k, "quads", len(quads), "lit", lit, "sha1", sha(img), "%.0fs" % (time.time() - t0), flush=True)
    json.dump(out, open(os.path.join(HERE, "my_digests_%s.json" % ("_".join(n[:3] for n in names) + "_" + "_".join(map(str, ks)))), "w"), indent=1)
