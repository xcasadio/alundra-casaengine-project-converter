"""E19.g G2e (step G2e-0): predictions of the effect-quad demo, committed before any engine code.

Plan: docs/plan-e19-opcodes.md, "E19.g G2e" (G2e-0) and "E19.g G3c" (which reuses the comparator rule).
The plan text names this file `g2e-predict.py`; it is `g2e_predict.py` (underscore) so that `g2e_compare.py` can import it.

Rule B(k) of the plan (screen-resolution rule of engine ADR-0068), exact integer arithmetic, no floating point decides a pixel:
  coverage : the TOP-LEFT corner of the screen pixel, (sx/k, sy/k) in PS1 units, against the quad = two triangles
             (TL, TR, BL) and (TR, BL, BR); an exact tie (the corner lies on an edge or a vertex) is decided by the
             TOP-LEFT RULE (verify.md R1): the corner belongs to a triangle iff, for each of its three edges, it is strictly
             inside or on the edge and the edge is a top edge (horizontal, interior below) or a left edge (interior to the
             right). Never the perturbation rule `tie_sign` of g2b1_predict.py (not imported, not used).
  texel    : floor(u(p) + 1/2) per axis, p = ((sx + 1/2)/k - 1/2, (sy + 1/2)/k - 1/2), u = affine map of the covering triangle,
             then clamped to the texture (PointClamp: a texel -1 reads column/row 0).
  blend    : texel alpha 0 = nothing drawn; mode None = replace; semi mode: alpha 255 replaces, alpha 128 (STP) blends:
             ABR0 (128 s + 127 d)/255 (engine AlphaBlend with alpha 128/255), ABR1 min(255, d + s), ABR2 max(0, d - s).
Noise zone (as G2b-1, per quad then united over the image): a covered pixel whose texel coordinate is within 2^-10 texel of a
boundary (exact ties EXCLUDED from the noise zone, i.e. kept), or any pixel whose top-left corner is within 2^-10 px of a
quad edge or of the TR-BL diagonal (exact distance 0 excluded, i.e. kept: decided by the top-left rule).

Usage:
  python g2e_predict.py                 writes g2e_expected.json and ref_g2e_<scene>_k<k>.png next to this file
  python g2e_predict.py --check         computes and prints, writes nothing
  python g2e_predict.py --check-g2b1    replays the G2b-1 cases under the top-left rule against g2b1_predictions.json
  python g2e_predict.py --check-source  checks the literal aura data against the export JSON (alundra-project/)
"""
import glob
import hashlib
import json
import math
import os
import sys
from fractions import Fraction as F

import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.abspath(os.path.join(HERE, '..', '..'))
G2B_ANNEX = os.path.join(REPO, 'docs', 'plan-e19-g2b-annexe')

# ---------------------------------------------------------------------------------------------------------------------
# The synthetic address texture (512 x 1105) and the frame
# ---------------------------------------------------------------------------------------------------------------------
TEX_W, TEX_H = 512, 1105
BG = (96, 112, 128)               # background colour
BG_TEXEL = (511, 0)               # the demo draws the background as a big sprite of this texel (as PsxFreeQuadDemo texel (15, 15))
PS1_W, PS1_H = 320, 240           # the compared frame is 320 k x 240 k pixels, the PS1 origin on the top-left pixel of the back-buffer
FACTORS = (1, 3)
NOISE_DEN = 1024                  # 2**-10
MARGIN_DEN = 16                   # probes keep 1/16 texel and 1/16 px from any boundary
PASS_YSORTED = 300                # RenderPass2D.YSortedWorld
LAYER = 300                       # sorting layer of the entities
ELEVATION = 214                   # 16 * 13 + 6: the elevation of the real aura (DepthKey >> 20 = 13)
BG_KEY = (PASS_YSORTED, 0, 0, 0, 0, 0, 0)     # the background sprite: layer 0, before every quad
MODE_NAMES = {None: 'None', 0: 'Mode0', 1: 'Mode1', 2: 'Mode2'}


def texel_rgba(tx, ty):
    """Texel formula (arrays or ints). R = 12 + 3 (i % 64), G = 12 + 3 (j % 64), B = 12 + (i // 64) * 18 + (j // 64): unique per
    texel (i < 512, j < 1105). Alpha: 0 where (i + 2 j) % 23 == 5; else 128 (STP) where (i + j) % 4 == 1; else 255. The background
    texel (511, 0) is (96, 112, 128, 255). Returns (r, g, b, a) as int64 arrays."""
    tx = np.asarray(tx, dtype=np.int64)
    ty = np.asarray(ty, dtype=np.int64)
    r = 12 + 3 * (tx % 64)
    g = 12 + 3 * (ty % 64)
    b = 12 + (tx // 64) * 18 + (ty // 64)
    a = np.where((tx + 2 * ty) % 23 == 5, 0, np.where((tx + ty) % 4 == 1, 128, 255))
    bgm = (tx == BG_TEXEL[0]) & (ty == BG_TEXEL[1])
    r = np.where(bgm, BG[0], r)
    g = np.where(bgm, BG[1], g)
    b = np.where(bgm, BG[2], b)
    a = np.where(bgm, 255, a)
    return r, g, b, a


# ---------------------------------------------------------------------------------------------------------------------
# The scenes
# ---------------------------------------------------------------------------------------------------------------------
# The real data: the 9 images of the 476 aura, effect 0 animation 1 frame 0 (image set 74, Idsv 52):
# (U, V, W, H, C = [x1, y1, x2, y2, x3, y3, x4, y4] = TL, TR, BL, BR relative to the anchor, Semi, Abr).
# Source: alundra-project/Maps/Lars & Melzas Room (beginning Event)-476/effects/*.effects.json, Effects[0].ImageSets[74]
# (see docs/formats/effects.md); checked by --check-source.
AURA = [
    (245, 97, 24, 72, [-65, -112, -57, -115, -17, 22, -9, 19], True, 1),
    (245, 97, 24, 72, [10, -118, 19, -117, -1, 24, 8, 25], True, 1),
    (0, 410, 64, 24, [-32, 8, 32, 8, -32, 32, 32, 32], False, 0),
    (405, 0, 64, 79, [-32, -47, 32, -47, -32, 32, 32, 32], True, 1),
    (0, 362, 47, 47, [-23, -31, 24, -31, -23, 16, 24, 16], True, 1),
    (270, 97, 24, 72, [-39, -114, -21, -117, -15, 27, 3, 23], True, 1),
    (360, 242, 64, 55, [-32, -15, 32, -15, -32, 40, 32, 40], True, 1),
    (144, 242, 71, 63, [-52, -28, 47, -28, -52, 60, 47, 60], True, 1),
    (0, 306, 64, 55, [-57, -37, 58, -37, -57, 62, 58, 62], True, 1),
]
AURA_ANCHOR = (160, 119)          # the real anchor on screen at scroll (800, -8) (G3C-R1)
AURA_COORD = 100 * 111            # 100 * the row of the world (G3C-R4)


def mirrored_corners(c, mx, my):
    """The corners of a true mirror: same silhouette, the texture mirrored (the data lists the TL vertex where the mirror put it).
    c = [TL, TR, BL, BR] as (x, y) pairs."""
    tl, tr, bl, br = c
    if mx:
        tl, tr, bl, br = tr, tl, br, bl
    if my:
        tl, tr, bl, br = bl, br, tl, tr
    return [tl, tr, bl, br]


def producer_flags(c):
    """The producer rule (G3C-R3): mx = C0 > C2, my = C1 > C5 (TL.x > TR.x, TL.y > BL.y)."""
    return int(c[0][0] > c[1][0]), int(c[0][1] > c[2][1])


def make_quad(qid, cell, corners, abr, key, origin):
    """cell = (U, V, W, H) of the atlas cell (the extractor's SourceX window); the raw window of the data steps a mirrored axis back
    one texel (the producer, G3C-R3). corners (x, y) PS1 y-down. abr None = opaque (mode None), 0..2 = ABR."""
    mx, my = producer_flags(corners)
    u, v, w, h = cell
    raw = (u - mx, v - my, w, h)
    return dict(id=qid, cell=list(cell), raw=list(raw), mirror=[mx, my], ps1_corners=[list(map(int, p)) for p in corners],
                world_corners=[[int(p[0]), -int(p[1])] for p in corners], abr=abr, mode=MODE_NAMES[abr], key=list(key), origin=origin)


def rect_corners(x0, y0, x1, y1, mx=0, my=0):
    return mirrored_corners([(x0, y0), (x1, y0), (x0, y1), (x1, y1)], mx, my)


def aura_scene(name, mixed):
    quads = []
    n = len(AURA)
    # synthetic assignment of (mirror X, mirror Y, ABR or None)
    mix = [(0, 0, 0), (1, 0, 1), (0, 0, None), (0, 1, 2), (1, 1, 1), (1, 0, 2), (0, 0, 0), (0, 1, 1), (1, 0, 2)]
    for i, (u, v, w, h, c, semi, abr) in enumerate(AURA):
        pts = [(AURA_ANCHOR[0] + c[2 * j], AURA_ANCHOR[1] + c[2 * j + 1]) for j in range(4)]
        if not mixed:
            assert producer_flags(pts) == (0, 0)
            a = abr if semi else None
            origin = 'real corners, real window, real mode (ABR1 or opaque), synthetic texture'
        else:
            mx, my, a = mix[i]
            pts = mirrored_corners(pts, mx, my)
            assert producer_flags(pts) == (mx, my), (i, producer_flags(pts), mx, my)
            origin = 'real silhouette and cell; SYNTHETIC mirror (%d, %d) and mode %s' % (mx, my, MODE_NAMES[a])
        key = (PASS_YSORTED, LAYER, 0, ELEVATION, AURA_COORD, n - 1 - i, 0)
        quads.append(make_quad('img%d' % i, (u, v, w, h), pts, a, key, origin))
    return dict(name=name, quads=quads)


def b_scene():
    """Mirrored images whose raw window origin is -1 (texel -1 clamps to column 0 / row 0, PointClamp). All synthetic. A base
    quad (opaque, unmirrored) lies under some of them so that the blended modes read a bright destination."""
    quads = []
    idx = [0]

    def add(cell, corners, abr, origin):
        idx[0] += 1
        quads.append(make_quad('b%d' % idx[0], cell, corners, abr, (PASS_YSORTED, LAYER, 0, ELEVATION, 0, 0, idx[0]), origin))

    # (b1) mirror X, magnified x3, cell U = 0 -> raw U = -1: the last pixels read texel -1
    add((0, 40, 8, 8), rect_corners(16, 16, 40, 40, 1, 0), None, 'mirror X, x3, raw U -1')
    # (b2) mirror Y, magnified x4, cell V = 0 -> raw V = -1
    add((100, 0, 8, 8), rect_corners(64, 16, 96, 48, 0, 1), None, 'mirror Y, x4, raw V -1')
    # (b3) mirror X and Y, x2, raw (-1, -1)
    add((0, 0, 16, 16), rect_corners(112, 16, 144, 48, 1, 1), None, 'mirror X+Y, x2, raw (-1, -1)')
    # (b4) mirror X, parallelogram, ABR1, raw U -1
    add((0, 200, 8, 8), mirrored_corners([(164, 16), (188, 16), (170, 40), (194, 40)], 1, 0), 1, 'mirror X, sheared, ABR1, raw U -1')
    # (b5) opaque base, unmirrored: the destination of the next three
    add((200, 200, 16, 16), rect_corners(16, 72, 80, 136), None, 'opaque base 4x')
    # (b6) mirror X trapezoid, ABR0 over the base, raw U -1
    add((0, 300, 8, 8), mirrored_corners([(20, 76), (44, 76), (16, 100), (52, 100)], 1, 0), 0, 'mirror X, trapezoid, ABR0, raw U -1')
    # (b7) mirror Y, deformed, ABR2 over the base, raw V -1
    add((50, 0, 8, 8), mirrored_corners([(48, 104), (76, 108), (50, 132), (74, 134)], 0, 1), 2, 'mirror Y, deformed, ABR2, raw V -1')
    # (b8) mirror X+Y, ABR1 over the base, raw (-1, -1)
    add((0, 0, 8, 8), mirrored_corners([(24, 112), (44, 112), (24, 128), (44, 128)], 1, 1), 1, 'mirror X+Y, ABR1, raw (-1, -1)')
    # (b9) 1:1 mirror X at U = 0 (the real case of 519 images): window -1 is never reached at k = 1
    add((0, 20, 24, 24), rect_corners(200, 80, 224, 104, 1, 0), None, 'mirror X, 1:1, raw U -1')
    # (b10) mirror Y at V = 0, magnified x3, ABR2 over an opaque neighbour (b11 drawn before), raw V -1
    add((300, 0, 16, 8), rect_corners(232, 120, 280, 144), None, 'opaque base 3x')
    add((300, 0, 16, 8), rect_corners(236, 124, 276, 140, 0, 1), 2, 'mirror Y, ABR2, raw V -1')
    # (b12) mirror X, minified x1/2 (window 32 wide, quad 16): raw U -1
    add((0, 600, 32, 32), rect_corners(200, 160, 216, 176, 1, 0), None, 'mirror X, minified, raw U -1')
    return dict(name='b_mirror_clamp', quads=quads)


def c_scene():
    """Exact coverage ties (integer vertices on slanted edges, vertex pixels) and texel-boundary ties near v ~ 1100 on the tall
    sheet (windows reach v = 1104). All synthetic."""
    quads = []
    idx = [0]

    def add(cell, corners, abr, origin):
        idx[0] += 1
        quads.append(make_quad('c%d' % idx[0], cell, corners, abr, (PASS_YSORTED, LAYER, 0, ELEVATION, 0, 0, idx[0]), origin))

    # (c1) x2 magnification, window bottom at v = 1104: a texel tie on every other row
    add((100, 1040, 64, 64), rect_corners(16, 16, 144, 144), None, 'x2, v 1040..1104')
    # (c2) x3 magnification, v 1080..1104
    add((300, 1080, 24, 24), rect_corners(160, 16, 232, 88), None, 'x3, v 1080..1104')
    # (c3) x1/2 minification (a texel tie every pixel), v 1040..1104
    add((360, 1040, 64, 64), rect_corners(16, 160, 48, 192), None, 'x1/2, v 1040..1104')
    # (c4) parallelogram of slope 1/4 (integer points on the slanted edges), ABR0, v 1056..1104
    add((100, 1056, 48, 48), [(100, 160), (148, 160), (112, 208), (160, 208)], 0, 'slope 1/4, ABR0, v 1056..1104')
    # (c5) trapezoid with 45 degree left edge and a vertex pixel on each corner, ABR1, v 1056..1104
    add((300, 1056, 52, 48), [(224, 160), (228, 160), (176, 208), (280, 208)], 1, 'trapezoid, ABR1, v 1056..1104')
    # (c6) shallow-edge quad (apex pixels at vertices where the perturbation rule differs), opaque, v 1088..1104
    add((200, 1088, 40, 16), [(100, 100), (150, 103), (98, 140), (152, 142)], None, 'shallow edges, v 1088..1104')
    # (c7) mirror X over c1, ABR2, window bottom at v = 1104: raw U = cell U - 1
    add((200, 1090, 12, 14), rect_corners(60, 60, 84, 88, 1, 0), 2, 'mirror X over c1, ABR2, v 1090..1104')
    # (c8) kite with a shallow right edge near the apex, ABR1 over c1 (apex pixel ties), v 1060..1100
    add((160, 1060, 32, 40), [(104, 20), (112, 21), (90, 60), (130, 63)], 1, 'kite, ABR1, v 1060..1100')
    return dict(name='c_ties_tall', quads=quads)


def build_scenes():
    return [aura_scene('a_aura_real', False), aura_scene('a_aura_mix', True), b_scene(), c_scene()]


# ---------------------------------------------------------------------------------------------------------------------
# Rule B(k), exact
# ---------------------------------------------------------------------------------------------------------------------

def sign(v):
    return (v > 0) - (v < 0)


def topleft_edge(p, q, r):
    """Edge p -> q of a triangle whose third vertex is r: True if it is a top edge (horizontal, interior below) or a left edge
    (interior to the right), y down."""
    if q[1] == p[1]:
        return r[1] > p[1]
    xe = F(p[0]) + F((r[1] - p[1]) * (q[0] - p[0]), (q[1] - p[1]))
    return F(r[0]) > xe


def triangles_of(q):
    tl, tr, bl, br = [tuple(p) for p in q['ps1_corners']]
    u, v, w, h = q['raw']
    return [((tl, tr, bl), [(u, v), (u + w, v), (u, v + h)]),
            ((tr, bl, br), [(u + w, v), (u, v + h), (u + w, v + h)])]


def seg_d2_exact(sx, sy, k, a, b):
    """Exact squared distance (PS1 px) from the point (sx/k, sy/k) to the segment [a, b]."""
    p = (F(sx, k), F(sy, k))
    ab = (b[0] - a[0], b[1] - a[1])
    ap = (p[0] - a[0], p[1] - a[1])
    l2 = ab[0] ** 2 + ab[1] ** 2
    t = (ap[0] * ab[0] + ap[1] * ab[1]) / l2
    if t <= 0:
        qx, qy = F(a[0]), F(a[1])
    elif t >= 1:
        qx, qy = F(b[0]), F(b[1])
    else:
        qx, qy = a[0] + t * ab[0], a[1] + t * ab[1]
    return (p[0] - qx) ** 2 + (p[1] - qy) ** 2


def seg_dist_float(px, py, a, b):
    abx, aby = float(b[0] - a[0]), float(b[1] - a[1])
    l2 = abx * abx + aby * aby
    t = np.clip(((px - a[0]) * abx + (py - a[1]) * aby) / l2, 0.0, 1.0)
    return np.hypot(px - (a[0] + t * abx), py - (a[1] + t * aby))


class QuadFields:
    """B(k) for one quad over a pixel box: covered, unclamped texel (tx, ty), ties, noise, margin flags."""


def quad_fields(corners, raw, k, box):
    """corners: [TL, TR, BL, BR] integer (x, y) PS1 y-down. raw = (Sx, Sy, w, h). box = (x0, y0, x1, y1) screen pixels [x0, x1) x [y0, y1)."""
    x0, y0, x1, y1 = box
    sx = np.arange(x0, x1, dtype=np.int64)[None, :]
    sy = np.arange(y0, y1, dtype=np.int64)[:, None]
    shape = (y1 - y0, x1 - x0)
    tl, tr, bl, br = [tuple(map(int, p)) for p in corners]
    u0, v0, w, h = raw
    tris = [((tl, tr, bl), [(u0, v0), (u0 + w, v0), (u0, v0 + h)]),
            ((tr, bl, br), [(u0 + w, v0), (u0, v0 + h), (u0 + w, v0 + h)])]
    f = QuadFields()
    f.box = box
    f.covered = np.zeros(shape, dtype=bool)
    f.tx = np.zeros(shape, dtype=np.int64)
    f.ty = np.zeros(shape, dtype=np.int64)
    f.tie_u = np.zeros(shape, dtype=bool)
    f.tie_v = np.zeros(shape, dtype=bool)
    f.noise = np.zeros(shape, dtype=bool)
    f.unsafe = np.zeros(shape, dtype=bool)      # a covered texel within 1/16 texel of a boundary (not a tie) or a corner within 1/16 px of an edge
    f.cov_tie = np.zeros(shape, dtype=bool)     # the corner lies on the boundary of a triangle
    for tri, uvs in tris:
        a, b, c = tri
        det = (b[0] - a[0]) * (c[1] - a[1]) - (b[1] - a[1]) * (c[0] - a[0])
        if det == 0:
            continue
        s = sign(det)
        inside = np.ones(shape, dtype=bool)
        closed = np.ones(shape, dtype=bool)
        anyzero = np.zeros(shape, dtype=bool)
        for p, q, r in ((a, b, c), (b, c, a), (c, a, b)):
            e = ((q[0] - p[0]) * (sy - k * p[1]) - (q[1] - p[1]) * (sx - k * p[0])) * s
            tl_edge = topleft_edge(p, q, r)
            inside &= (e > 0) | ((e == 0) & tl_edge)
            closed &= e >= 0
            anyzero |= e == 0
        f.cov_tie |= closed & anyzero
        new = inside & ~f.covered
        if not new.any():
            continue
        px = 2 * sx + 1 - k          # p = P / (2 k)
        py = 2 * sy + 1 - k
        n1 = (px - 2 * k * a[0]) * (c[1] - a[1]) - (c[0] - a[0]) * (py - 2 * k * a[1])
        n2 = (b[0] - a[0]) * (py - 2 * k * a[1]) - (px - 2 * k * a[0]) * (b[1] - a[1])
        D = 2 * k * det
        sg = sign(D)
        D = abs(D)
        n1 = n1 * sg
        n2 = n2 * sg
        for axis in (0, 1):
            ua, ub, uc = uvs[0][axis], uvs[1][axis], uvs[2][axis]
            num = 2 * (ua * D + n1 * (ub - ua) + n2 * (uc - ua)) + D       # (u + 1/2) * 2 D
            den = 2 * D
            t = np.floor_divide(num, den)
            r = np.mod(num, den)
            noise_t = (r > 0) & ((r * NOISE_DEN < den) | ((den - r) * NOISE_DEN < den))
            tie_t = r == 0
            unsafe_t = (r > 0) & ((r * MARGIN_DEN < den) | ((den - r) * MARGIN_DEN < den))
            if axis == 0:
                f.tx = np.where(new, t, f.tx)
                f.tie_u |= new & tie_t
            else:
                f.ty = np.where(new, t, f.ty)
                f.tie_v |= new & tie_t
            f.noise |= new & noise_t
            f.unsafe |= new & unsafe_t
        f.covered |= new
    # edge noise: the corner within 2**-10 px of an edge or of the diagonal (exact distance 0 excluded)
    segs = [(tl, tr), (tr, br), (br, bl), (bl, tl), (tr, bl)]
    pxf = sx / float(k)
    pyf = sy / float(k)
    dmin = np.full(shape, 1e9)
    for a, b in segs:
        dmin = np.minimum(dmin, seg_dist_float(pxf, pyf, a, b))
    f.unsafe |= dmin < 1.0 / MARGIN_DEN
    cand = np.argwhere(dmin < 2.0 / NOISE_DEN)
    lim = F(1, NOISE_DEN * NOISE_DEN)
    for j, i in cand:
        gx, gy = x0 + int(i), y0 + int(j)
        best = None
        for a, b in segs:
            d2 = seg_d2_exact(gx, gy, k, a, b)
            if best is None or d2 < best:
                best = d2
        if 0 < best < lim:
            f.noise[j, i] = True
    return f


def quad_box(corners, k, W, H):
    xs = [p[0] for p in corners]
    ys = [p[1] for p in corners]
    x0, x1 = max(0, k * (min(xs) - 2)), min(W, k * (max(xs) + 2))
    y0, y1 = max(0, k * (min(ys) - 2)), min(H, k * (max(ys) + 2))
    return (x0, y0, x1, y1)


def perturbation_cover(corners, k, box):
    """Informational only: the coverage of the OLD tie rule (perturbation of the sample point, g2b1 `tie_sign`), reimplemented
    here to count the pixels where it differs from the top-left rule. It decides nothing."""
    x0, y0, x1, y1 = box
    sx = np.arange(x0, x1, dtype=np.int64)[None, :]
    sy = np.arange(y0, y1, dtype=np.int64)[:, None]
    shape = (y1 - y0, x1 - x0)
    tl, tr, bl, br = [tuple(map(int, p)) for p in corners]
    cov = np.zeros(shape, dtype=bool)
    for tri in ((tl, tr, bl), (tr, bl, br)):
        sgn = []
        for p, q in ((tri[0], tri[1]), (tri[1], tri[2]), (tri[2], tri[0])):
            e = (q[0] - p[0]) * (sy - k * p[1]) - (q[1] - p[1]) * (sx - k * p[0])
            tie = (q[0] - p[0]) * 10 ** 9 - (q[1] - p[1]) * (10 ** 9 + 7)
            sgn.append(np.where(e != 0, np.sign(e), sign(tie)))
        cov |= ((sgn[0] > 0) & (sgn[1] > 0) & (sgn[2] > 0)) | ((sgn[0] < 0) & (sgn[1] < 0) & (sgn[2] < 0))
    return cov


# ---------------------------------------------------------------------------------------------------------------------
# One scene at one factor
# ---------------------------------------------------------------------------------------------------------------------

def blend(abr, s, d):
    if abr == 0:
        return (2 * (128 * s + 127 * d) + 255) // 510
    if abr == 1:
        return np.minimum(255, s + d)
    if abr == 2:
        return np.maximum(0, d - s)
    raise ValueError(abr)


def render_scene(scene, k):
    W, H = PS1_W * k, PS1_H * k
    img = np.empty((H, W, 3), dtype=np.int64)
    img[:, :] = BG
    tol = np.zeros((H, W), dtype=np.uint8)           # 1 where the value is the result of a blend
    noise = np.zeros((H, W), dtype=bool)
    unsafe = np.zeros((H, W), dtype=bool)
    tie_vis = np.zeros((H, W), dtype=bool)           # the visible value depends on a texel tie
    tie_any = np.zeros((H, W), dtype=bool)
    tie_v1090 = np.zeros((H, W), dtype=bool)
    cov_tie = np.zeros((H, W), dtype=bool)
    cov_tie_cov = np.zeros((H, W), dtype=bool)
    clamped = np.zeros((H, W), dtype=bool)
    rule_diff = np.zeros((H, W), dtype=bool)
    last_clamp = np.zeros((H, W), dtype=bool)
    quads = sorted(scene['quads'], key=lambda q: tuple(q['key']))
    for q in quads:
        corners = q['ps1_corners']
        box = quad_box(corners, k, W, H)
        x0, y0, x1, y1 = box
        if x1 <= x0 or y1 <= y0:
            continue
        f = quad_fields(corners, q['raw'], k, box)
        sl = (slice(y0, y1), slice(x0, x1))
        cx = np.clip(f.tx, 0, TEX_W - 1)
        cy = np.clip(f.ty, 0, TEX_H - 1)
        r, g, b, a = texel_rgba(cx, cy)
        bgtex = f.covered & (cx == BG_TEXEL[0]) & (cy == BG_TEXEL[1])
        assert not bgtex.any(), 'a quad reads the background texel: ' + q['id']
        draw = f.covered & (a > 0)
        semi = q['abr'] is not None
        replace = draw & ((a == 255) | (not semi)) if semi else draw
        blended = draw & (a == 128) & semi
        reg = img[sl]
        src = np.stack([r, g, b], axis=-1)
        if semi:
            bl = blend(q['abr'], src, reg)
            reg = np.where(blended[..., None], bl, reg)
        reg = np.where(replace[..., None], src, reg)
        img[sl] = reg
        t = tol[sl]
        t = np.where(replace, 0, t)
        t = np.where(blended, 1, t)
        tol[sl] = t
        tie = (f.tie_u | f.tie_v) & ~f.noise
        tv = tie_vis[sl]
        tv = np.where(replace, tie, tv)
        tv = np.where(blended, tv | tie, tv)
        tie_vis[sl] = tv
        tie_any[sl] |= f.covered & (f.tie_u | f.tie_v)
        tie_v1090[sl] |= f.covered & f.tie_v & (f.ty >= 1090)
        cov_tie[sl] |= f.cov_tie
        cov_tie_cov[sl] |= f.cov_tie & f.covered
        noise[sl] |= f.noise
        unsafe[sl] |= f.unsafe
        outside = (f.tx < 0) | (f.ty < 0) | (f.tx >= TEX_W) | (f.ty >= TEX_H)
        clamped[sl] |= f.covered & outside
        rule_diff[sl] |= perturbation_cover(corners, k, box) != f.covered
    return dict(img=img, tol=tol, noise=noise, unsafe=unsafe, tie_vis=tie_vis, tie_any=tie_any, tie_v1090=tie_v1090, cov_tie=cov_tie,
                cov_tie_cov=cov_tie_cov, clamped=clamped, rule_diff=rule_diff, k=k, W=W, H=H)


def digest(img):
    return hashlib.sha1(img.astype(np.uint8).tobytes()).hexdigest()


def summarize(res):
    img, noise = res['img'], res['noise']
    lit = (img != np.array(BG)).any(axis=2)
    keep = ~noise
    return dict(
        width=res['W'], height=res['H'], k=res['k'],
        sha1=digest(img),
        lit_pixels=int(lit.sum()),
        noise_pixels=int(noise.sum()),
        noise_sha1=hashlib.sha1(np.packbits(noise).tobytes()).hexdigest(),
        blended_pixels=int((res['tol'] == 1).sum()),
        exact_ties_kept=int((res['tie_any'] & keep).sum()),            # pixels with a texel tie in some covered draw, not noise
        texel_ties_visible=int((res['tie_vis'] & keep).sum()),          # ... whose visible value depends on it
        texel_ties_v_ge_1090=int((res['tie_v1090'] & keep).sum()),      # ... v tie with a texel row >= 1090 (tall sheet)
        coverage_ties_on_boundary=int((res['cov_tie'] & keep).sum()),   # corner exactly on a triangle boundary (decided by the top-left rule)
        coverage_ties_covered=int((res['cov_tie_cov'] & keep).sum()),
        clamped_pixels=int((res['clamped'] & keep).sum()),              # covered pixels reading a texel outside the sheet (clamped)
        pixels_where_old_tie_rule_differs=int(res['rule_diff'].sum()),  # informational: perturbation rule vs top-left rule (coverage)
    )


def pick_probes(res, scene):
    """Probes: pixels outside the noise zone with margins >= 1/16 texel and 1/16 px in every draw touching them (not ties), a
    deterministic spread of 8; plus up to 4 clamped pixels, 4 visible tie pixels (kept, exact). Each: x, y, color, tol, kind."""
    img, tol = res['img'], res['tol']
    keep = ~res['noise']
    lit = (img != np.array(BG)).any(axis=2)
    out = []

    def add(mask, n, kind):
        pts = np.argwhere(mask)
        if len(pts) == 0:
            return
        step = max(1, len(pts) // n)
        for j, i in pts[::step][:n]:
            out.append(dict(x=int(i), y=int(j), color=[int(c) for c in img[j, i]], tol=int(tol[j, i]), kind=kind))

    add(lit & keep & ~res['unsafe'] & ~res['tie_any'] & (res['tol'] == 0), 5, 'margin-opaque')
    add(lit & keep & ~res['unsafe'] & ~res['tie_any'] & (res['tol'] == 1), 3, 'margin-blended')
    add(res['clamped'] & keep & ~res['tie_any'], 4, 'clamped')
    add(res['tie_vis'] & keep, 4, 'texel-tie')
    add(res['cov_tie_cov'] & keep, 3, 'coverage-tie')
    add(~lit & keep & ~res['unsafe'] & ~res['cov_tie'], 2, 'background')
    return out


# ---------------------------------------------------------------------------------------------------------------------
# G2b-1 guard
# ---------------------------------------------------------------------------------------------------------------------

def check_g2b1(verbose=True):
    """Replays the G2b-1 quad cases under the top-left rule against the G2b-1 predictions (texel codes, noise): 0 differing pixel expected."""
    with open(os.path.join(G2B_ANNEX, 'g2b1_predictions.json'), encoding='utf-8') as fh:
        pred = json.load(fh)
    tex = pred['texture']['size']
    total = bad_code = bad_noise = checked = 0
    for case in pred['cases']:
        if not case.get('corners'):
            continue                      # the rectangle path rows (b): not a quad
        for ks, info in case['per_k'].items():
            k = int(ks)
            box = tuple(info['box'])
            f = quad_fields([tuple(p) for p in case['corners']], case['raw'], k, box)
            codes = np.where(f.covered, f.tx * tex + f.ty, -1).reshape(-1)
            exp_codes = np.array(info['texel_codes'])
            exp_noise = np.array(info['noise'], dtype=bool)
            dc = int((codes != exp_codes).sum())
            dn = int((f.noise.reshape(-1) != exp_noise).sum())
            total += len(codes)
            bad_code += dc
            bad_noise += dn
            checked += 1
            if verbose:
                print('  %-14s k=%d box=%s pixels=%d covered=%d texel/coverage differ=%d noise differ=%d'
                      % (case['name'], k, list(box), len(codes), int(f.covered.sum()), dc, dn))
    print('G2b-1 guard (top-left rule): %d case-factor pairs, %d pixels, %d differing texel/coverage pixels, %d differing noise pixels'
          % (checked, total, bad_code, bad_noise))
    return bad_code + bad_noise


def check_source():
    paths = [p for p in glob.glob(os.path.join(REPO, 'alundra-project', 'Maps', '*', '*-476', 'effects', '*.effects.json'))]
    if not paths:
        print('export JSON not found')
        return 1
    with open(paths[0], encoding='utf-8') as fh:
        doc = json.load(fh)
    table = doc['Effects'][0]
    frame = table['Animations'][1]['Frames'][0]
    imset = table['ImageSets'][frame[1]]
    got = [(im['U'], im['V'], im['W'], im['H'], im['C'], im['Semi'], im['Abr']) for im in imset['Images']]
    ok = [list(g) for g in got] == [list(a) for a in AURA]
    print('aura literal equals the export (Effects[0] animation 1 frame 0, image set %d, Idsv %d): %s' % (frame[1], imset['Idsv'], ok))
    return 0 if ok else 1


# ---------------------------------------------------------------------------------------------------------------------
# Output
# ---------------------------------------------------------------------------------------------------------------------

def build_expected(write_png):
    scenes = build_scenes()
    out = dict(
        description='E19.g G2e-0 predictions of the demo EffectQuadDemo (rule B(k), top-left tie rule). Generated by g2e_predict.py.',
        texture=dict(width=TEX_W, height=TEX_H,
                     formula=('texel (i, j): R = 12 + 3 (i % 64); G = 12 + 3 (j % 64); B = 12 + (i // 64) * 18 + (j // 64); '
                              'alpha = 0 if (i + 2 j) % 23 == 5, else 128 if (i + j) % 4 == 1, else 255; texel (511, 0) = background (96, 112, 128, 255); '
                              'straight (non-premultiplied) alpha, SurfaceFormat.Color, PointClamp')),
        frame=dict(ps1_size=[PS1_W, PS1_H], factors=list(FACTORS), background=list(BG), background_texel=list(BG_TEXEL),
                   background_key=list(BG_KEY),
                   note=('image = the top-left 320 k x 240 k pixels of the back-buffer (>= that size); camera Zoom = k, Target = (w / 2k, -h / 2k) '
                         'of the back-buffer w x h, so a PS1 point (x, y) is the screen point (k x, k y); background = a sprite of texel (511, 0) '
                         'covering the frame; every quad drawn with Color.White, z = 0')),
        noise_rule=('texel within 2^-10 texel of a boundary (exact ties excluded, i.e. kept) or pixel top-left corner within 2^-10 px of an '
                    'edge or the TR-BL diagonal (exact distance 0 excluded, i.e. kept); exact ties are decided by the top-left rule'),
        tolerance_rule='opaque and background pixels exact; a pixel last written by a blended STP draw (modes Mode0 to Mode2) +-1 per channel',
        scenes=[])
    for sc in scenes:
        entry = dict(name=sc['name'], quads=sc['quads'], per_k={})
        for k in FACTORS:
            res = render_scene(sc, k)
            info = summarize(res)
            info['probes'] = pick_probes(res, sc)
            entry['per_k'][str(k)] = info
            if write_png:
                from PIL import Image
                Image.fromarray(res['img'].astype(np.uint8), 'RGB').save(os.path.join(HERE, 'ref_g2e_%s_k%d.png' % (sc['name'], k)), optimize=True)
        out['scenes'].append(entry)
    return out


def main():
    if '--check-g2b1' in sys.argv:
        sys.exit(1 if check_g2b1() else 0)
    if '--check-source' in sys.argv:
        sys.exit(check_source())
    check = '--check' in sys.argv
    out = build_expected(write_png=not check)
    for e in out['scenes']:
        for ks, i in e['per_k'].items():
            print('%-14s k=%s %dx%d sha1=%s lit=%d noise=%d blended=%d ties_kept=%d (visible %d, v>=1090 %d) cov_ties=%d (covered %d) clamped=%d old_rule_differs=%d probes=%d'
                  % (e['name'], ks, i['width'], i['height'], i['sha1'], i['lit_pixels'], i['noise_pixels'], i['blended_pixels'],
                     i['exact_ties_kept'], i['texel_ties_visible'], i['texel_ties_v_ge_1090'], i['coverage_ties_on_boundary'],
                     i['coverage_ties_covered'], i['clamped_pixels'], i['pixels_where_old_tie_rule_differs'], len(i['probes'])))
    if not check:
        with open(os.path.join(HERE, 'g2e_expected.json'), 'w', newline='\n', encoding='utf-8') as fh:
            json.dump(out, fh, indent=1)
            fh.write('\n')


if __name__ == '__main__':
    main()
