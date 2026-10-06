"""Coverage of random PS1 quads (integer vertices): hardware walker port R vs top-left rule (mine) vs perturbation rule (the report's B tie rule)."""
import random
from fractions import Fraction as F
import redux_nomask as R
import g2b1_predict as B
import my_render as M
random.seed(99)
tot = 0; dm = 0; dB = 0; both = 0; quads = 0; maxc = 0
for _ in range(1500):
    # a general quad with integer vertices in 0..30 (TL TR BL BR), keep it non-degenerate-ish
    pts = [(random.randint(0, 30), random.randint(0, 30)) for _ in range(4)]
    w = h = 8
    try:
        px = R.quad(pts, [(0, 0), (w, 0), (0, h), (w, h)])
    except Exception:
        continue
    quads += 1
    Rset = set(px.keys())
    tl, tr, bl, br = [(F(x), F(y)) for x, y in pts]
    tris = [(tl, tr, bl), (tr, bl, br)]
    for sy in range(-1, 32):
        for sx in range(-1, 32):
            mine = any(M.inside(t, (F(sx), F(sy))) for t in tris)
            bb = any(B.inside(t, sx, sy, 1) for t in tris)
            r = (sx, sy) in Rset
            tot += 1
            dm += (mine != r); dB += (bb != r)
print("quads", quads, "pixels", tot, "R-vs-mine coverage mismatches", dm, " R-vs-perturbation mismatches", dB)
