"""E19.g G2b-1 (G2b1-0): predictions of the free-quad scene, committed before any engine code.

Two models of the same quads, in PS1 coordinates (y down, pixels of the PS1):
  B(k)  the screen-resolution rule of the plan (exact rationals, no floating point):
        coverage = top-left corner of the screen pixel (sx/k, sy/k) against the quad (two triangles, top-left rule, the
        PCSX-Redux split TR-BL), texel = floor(u(p) + 1/2) with p = ((sx+1/2)/k - 1/2, (sy+1/2)/k - 1/2), u the affine map of the
        triangle that covers.
  R     the port of the PCSX-Redux software GPU walker (redux.py, validated on 49 hardware probes), at k = 1 only.
Also here: the rectangle path of today (coverage = pixel centre in [L,R)x[T,B), texel = window column, mirrored axis flipped),
used by the two 1:1 rows; the noise zone; the probes; the mutation table.

Run:  python g2b1_predict.py            writes g2b1_predictions.json and g2b1-predictions.md next to this file.
      python g2b1_predict.py --check    only prints the summary (no files).
Nothing here reads the engine; the demo (G2b1-2) is compared with g2b1_predictions.json by g2b1_compare.py.
"""
import json
import math
import os
import sys
from fractions import Fraction as F

import redux

HERE = os.path.dirname(os.path.abspath(__file__))
TEX = 16                      # the address texture is 16 x 16 texels
BG = (100, 150, 200)          # background colour, texel (15, 15)
NOISE = F(1, 1024)            # 2**-10
MARGIN = F(1, 16)             # probes keep at least 1/16 texel / 1/16 px from any boundary
FACTORS = (1, 3)


def texel_color(i, j):
    """Unique opaque colour per texel (R, G carry the coordinates), alpha 255 or 128 (STP when (i + j) % 4 == 1)."""
    if (i, j) == (15, 15):
        return (BG[0], BG[1], BG[2], 255)
    alpha = 128 if (i + j) % 4 == 1 else 255
    return (20 + 14 * i, 20 + 14 * j, 60 + ((i * 7 + j * 3) % 11) * 10, alpha)


# PS1 quads. corners = HG, HD, BG, BD (data order TL, TR, BL, BR), y down. raw = (Sx, Sy, w, h): the raw UV window of the
# data, uv = (Sx, Sy), (Sx + w, Sy), (Sx, Sy + h), (Sx + w, Sy + h). mode: None, 0 or 1.
CASES = [
    dict(name='magnify4', corners=[(8, 8), (40, 8), (8, 40), (40, 40)], raw=(0, 0, 8, 8), mode=None),
    dict(name='mirror1.5', corners=[(62, 8), (50, 8), (62, 20), (50, 20)], raw=(0, 0, 8, 8), mode=None),
    dict(name='parallelogram', corners=[(70, 8), (94, 8), (76, 32), (100, 32)], raw=(0, 0, 8, 8), mode=None),
    dict(name='trapezoid', corners=[(112, 8), (132, 8), (108, 32), (140, 32)], raw=(0, 0, 8, 8), mode=None),
    dict(name='mode1', corners=[(8, 56), (20, 56), (10, 68), (22, 68)], raw=(0, 0, 8, 8), mode=1),
    dict(name='mode0', corners=[(40, 56), (52, 56), (42, 68), (54, 68)], raw=(0, 0, 8, 8), mode=0),
    dict(name='mirrorX1to1', corners=[(88, 56), (80, 56), (88, 64), (80, 64)], raw=(0, 0, 8, 8), mode=None),
    dict(name='mirrorY1to1', corners=[(104, 64), (112, 64), (104, 56), (112, 56)], raw=(0, 0, 8, 8), mode=None),
    # the two 1:1 rows: (a) by the quad, (b) by the rectangle path of today (window = SourceX/SourceY of the extractor)
    dict(name='row_plain_a', corners=[(8, 88), (16, 88), (8, 96), (16, 96)], raw=(2, 3, 8, 8), mode=None),
    dict(name='row_plain_b', rect=dict(L=24, T=88, w=8, h=8, src=(2, 3), mx=False, my=False), mode=None),
    dict(name='row_mx_a', corners=[(48, 88), (40, 88), (48, 96), (40, 96)], raw=(2, 3, 8, 8), mode=None),
    dict(name='row_mx_b', rect=dict(L=56, T=88, w=8, h=8, src=(3, 3), mx=True, my=False), mode=None),
    dict(name='row_my_a', corners=[(72, 96), (80, 96), (72, 88), (80, 88)], raw=(2, 3, 8, 8), mode=None),
    dict(name='row_my_b', rect=dict(L=88, T=88, w=8, h=8, src=(2, 4), mx=False, my=True), mode=None),
]
ROW_PAIRS = [('row_plain_a', 'row_plain_b'), ('row_mx_a', 'row_mx_b'), ('row_my_a', 'row_my_b')]


def case_uvs(case):
    sx, sy, w, h = case['raw']
    return [(sx, sy), (sx + w, sy), (sx, sy + h), (sx + w, sy + h)]


def case_box(case):
    if 'rect' in case:
        r = case['rect']
        return r['L'], r['T'], r['L'] + r['w'], r['T'] + r['h']
    xs = [c[0] for c in case['corners']]
    ys = [c[1] for c in case['corners']]
    return min(xs), min(ys), max(xs), max(ys)


def case_region(case, k):
    x0, y0, x1, y1 = case_box(case)
    return k * (x0 - 2), k * (y0 - 2), k * (x1 + 2), k * (y1 + 2)   # screen pixels [x0, x1) x [y0, y1)


def is_mirrored(case):
    if 'rect' in case:
        return False
    (x1, y1), (x2, y2), (x3, y3), (x4, y4) = case['corners']
    return x1 > x2 or y1 > y3


# ---- exact rational helpers (integer fast path for coverage) ----------------------------------------------------------

def edge_scaled(a, b, sx, sy, k):
    """k * edge(a, b, c) with c = (sx/k, sy/k): all integers."""
    return (b[0] - a[0]) * (sy - k * a[1]) - (b[1] - a[1]) * (sx - k * a[0])


def tie_sign(a, b):
    """Sign of the edge function under the tiny perturbation of the sample (1e-9, 1/(1e9+7)): data-verify, Redux coverage."""
    t = (b[0] - a[0]) * 10**9 - (b[1] - a[1]) * (10**9 + 7)
    return (t > 0) - (t < 0)


def sgn_edge(a, b, sx, sy, k):
    e = edge_scaled(a, b, sx, sy, k)
    if e != 0:
        return (e > 0) - (e < 0)
    return tie_sign(a, b)


def inside(tri, sx, sy, k):
    a, b, c = tri
    s0, s1, s2 = sgn_edge(a, b, sx, sy, k), sgn_edge(b, c, sx, sy, k), sgn_edge(c, a, sx, sy, k)
    return (s0 > 0 and s1 > 0 and s2 > 0) or (s0 < 0 and s1 < 0 and s2 < 0)


def edge_f(a, b, p):
    return (b[0] - a[0]) * (p[1] - a[1]) - (b[1] - a[1]) * (p[0] - a[0])


def affine(tri, uvs, p):
    a, b, c = tri
    det = edge_f(a, b, c)
    w1 = edge_f(b, c, p) / det
    w2 = edge_f(c, a, p) / det
    w3 = 1 - w1 - w2
    return (w1 * uvs[0][0] + w2 * uvs[1][0] + w3 * uvs[2][0], w1 * uvs[0][1] + w2 * uvs[1][1] + w3 * uvs[2][1])


def triangles(case, mut):
    tl, tr, bl, br = [(F(x), F(y)) for x, y in case['corners']]
    uvs = case_uvs(case)
    if mut.get('no_recul') and is_mirrored(case):
        (x1, y1), (x2, y2), (x3, y3), _ = case['corners']
        du = 1 if x1 > x2 else 0
        dv = 1 if y1 > y3 else 0
        uvs = [(u + du, v + dv) for u, v in uvs]
    if mut.get('other_diagonal'):
        return [((tl, tr, br), (uvs[0], uvs[1], uvs[3])), ((tl, br, bl), (uvs[0], uvs[3], uvs[2]))]
    return [((tl, tr, bl), (uvs[0], uvs[1], uvs[2])), ((tr, bl, br), (uvs[1], uvs[2], uvs[3]))]


def bk(case, k, sx, sy, mut=None):
    """(texel, covered): the texel (tx, ty) or None, for the screen pixel (sx, sy). mut: dict of mutations (see MUTATIONS)."""
    mut = mut or {}
    if 'rect' in case:
        return rect_texel(case['rect'], k, sx, sy)
    for tri, uvs in triangles(case, mut):
        if mut.get('center_coverage'):
            ok = inside_center(tri, sx, sy, k)
        else:
            ok = inside(tri, sx, sy, k)
        if ok:
            if mut.get('center_coverage'):
                # geometry not shifted: the sample is the pixel centre, the shader still removes its slope term
                c = (F(2 * sx + 1, 2 * k), F(2 * sy + 1, 2 * k))
                half = F(1, 2) - F(1, 2 * k)
                p = (c[0] - half, c[1] - half)
            elif mut.get('no_slope'):
                p = (F(sx, k), F(sy, k))
            else:
                p = (F(2 * sx + 1, 2 * k) - F(1, 2), F(2 * sy + 1, 2 * k) - F(1, 2))
            u, v = affine(tri, uvs, p)
            if mut.get('no_half_texel'):
                return (math.floor(u), math.floor(v))
            return (math.floor(u + F(1, 2)), math.floor(v + F(1, 2)))
    return None


def inside_center(tri, sx, sy, k):
    a, b, c = tri
    cx, cy = F(2 * sx + 1, 2 * k), F(2 * sy + 1, 2 * k)
    q = (cx + F(1, 10**9), cy + F(1, 10**9 + 7))
    e0, e1, e2 = edge_f(a, b, q), edge_f(b, c, q), edge_f(c, a, q)
    return (e0 > 0 and e1 > 0 and e2 > 0) or (e0 < 0 and e1 < 0 and e2 < 0)


def rect_texel(r, k, sx, sy):
    """The rectangle path of today: coverage = pixel centre in [L, R) x [T, B), window column floor(centre - L), flipped on a
    mirrored axis; src = the window (SourceX, SourceY) of the extractor."""
    cx, cy = F(2 * sx + 1, 2 * k), F(2 * sy + 1, 2 * k)
    if not (r['L'] <= cx < r['L'] + r['w'] and r['T'] <= cy < r['T'] + r['h']):
        return None
    i, j = math.floor(cx - r['L']), math.floor(cy - r['T'])
    return (r['src'][0] + (r['w'] - 1 - i if r['mx'] else i), r['src'][1] + (r['h'] - 1 - j if r['my'] else j))


def texel_position(case, k, sx, sy):
    """(u + 1/2, v + 1/2) of B(k) for the covering triangle, or None (used for the distance to a texel boundary)."""
    if 'rect' in case:
        return None
    for tri, uvs in triangles(case, {}):
        if inside(tri, sx, sy, k):
            p = (F(2 * sx + 1, 2 * k) - F(1, 2), F(2 * sy + 1, 2 * k) - F(1, 2))
            u, v = affine(tri, uvs, p)
            return (u + F(1, 2), v + F(1, 2))
    return None


def frac_distance(x):
    f = x - math.floor(x)
    return min(f, 1 - f)


def seg_dist2(p, a, b):
    """Squared distance from p to the segment [a, b], exact."""
    ab = (b[0] - a[0], b[1] - a[1])
    ap = (p[0] - a[0], p[1] - a[1])
    l2 = ab[0] ** 2 + ab[1] ** 2
    t = (ap[0] * ab[0] + ap[1] * ab[1]) / l2
    if t <= 0:
        q = a
    elif t >= 1:
        q = b
    else:
        q = (a[0] + t * ab[0], a[1] + t * ab[1])
    return (p[0] - q[0]) ** 2 + (p[1] - q[1]) ** 2


def edge_segments(case):
    if 'rect' in case:
        r = case['rect']
        l, t, rr, b = r['L'], r['T'], r['L'] + r['w'], r['T'] + r['h']
        return [((l, t), (rr, t)), ((rr, t), (rr, b)), ((rr, b), (l, b)), ((l, b), (l, t))]
    tl, tr, bl, br = case['corners']
    return [(tl, tr), (tr, br), (br, bl), (bl, tl), (tr, bl)]


def edge_distance2(case, k, sx, sy):
    """Smallest squared distance from the top-left corner of the pixel to a boundary or the diagonal of the quad (PS1 px)."""
    p = (F(sx, k), F(sy, k))
    best = None
    for a, b in edge_segments(case):
        # cheap reject on the bounding box of the segment grown by 1/64
        if not (min(a[0], b[0]) - 0.02 <= float(p[0]) <= max(a[0], b[0]) + 0.02
                and min(a[1], b[1]) - 0.02 <= float(p[1]) <= max(a[1], b[1]) + 0.02):
            continue
        d = seg_dist2((p[0], p[1]), (F(a[0]), F(a[1])), (F(b[0]), F(b[1])))
        if best is None or d < best:
            best = d
    return best


# ---- the image of a case ---------------------------------------------------------------------------------------------

def image(case, k, mut=None):
    """dict with: box (x0, y0, x1, y1), texel[(sx, sy)] = (tx, ty) | None, tex_dist[(sx, sy)] = min texel boundary distance
    (Fraction, only when B is a quad covered pixel), edge_d2[(sx, sy)]."""
    x0, y0, x1, y1 = case_region(case, k)
    out = {}
    for sy in range(y0, y1):
        for sx in range(x0, x1):
            out[(sx, sy)] = bk(case, k, sx, sy, mut)
    return (x0, y0, x1, y1), out


def noise_and_distance(case, k, base):
    """noise[(sx, sy)] True when the pixel is in the noise zone; kept_min = smallest texel-boundary distance among kept pixels."""
    (x0, y0, x1, y1), tex = base
    noise = {}
    kept_min = None
    kept_exact = 0
    in_band = 0          # kept pixels with 2**-10 <= distance < 2**-8 (what a sampler of 8 sub-texel bits could flip)
    for (sx, sy), t in tex.items():
        n = False
        d2 = edge_distance2(case, k, sx, sy)
        if d2 is not None and 0 < d2 < NOISE * NOISE:
            n = True
        if t is not None and not n and 'rect' not in case:
            pos = texel_position(case, k, sx, sy)
            if pos is not None:
                dx, dy = frac_distance(pos[0]), frac_distance(pos[1])
                for d in (dx, dy):
                    if 0 < d < NOISE:
                        n = True
                if not n:
                    for d in (dx, dy):
                        if d == 0:
                            kept_exact += 1
                        else:
                            if kept_min is None or d < kept_min:
                                kept_min = d
                            if d < F(1, 256):
                                in_band += 1
        noise[(sx, sy)] = n
    return noise, kept_min, kept_exact, in_band


def margins_ok(case, k, sx, sy):
    d2 = edge_distance2(case, k, sx, sy)
    if d2 is not None and d2 < MARGIN * MARGIN:
        return False
    if 'rect' in case:
        return True
    pos = texel_position(case, k, sx, sy)
    if pos is None:
        return False
    return frac_distance(pos[0]) >= MARGIN and frac_distance(pos[1]) >= MARGIN


# ---- colours ---------------------------------------------------------------------------------------------------------

def expected_color(case, texel):
    """Colour the demo must read for a covered pixel with this texel; (r, g, b, tolerance)."""
    if texel is None:
        return (BG[0], BG[1], BG[2], 0)
    r, g, b, a = texel_color(*texel)
    if a == 255 or case['mode'] is None:
        return (r, g, b, 0)
    if case['mode'] == 0:
        return ((r + BG[0]) // 2, (g + BG[1]) // 2, (b + BG[2]) // 2, 1)
    if case['mode'] == 1:
        return (min(255, r + BG[0]), min(255, g + BG[1]), min(255, b + BG[2]), 1)
    raise ValueError


# ---- mutations -------------------------------------------------------------------------------------------------------

MUTATIONS = {
    'other_diagonal': dict(other_diagonal=True),
    'no_half_pixel_geometry': dict(center_coverage=True),
    'no_half_texel': dict(no_half_texel=True),
    'no_mirror_recul': dict(no_recul=True),
    'no_slope_term': dict(no_slope=True),
}


def build():
    result = dict(texture=dict(size=TEX, background=BG), cases=[], rows=[], mutations={}, summary={})
    probe_pool = {}
    for case in CASES:
        entry = dict(name=case['name'], mode=case['mode'], rect=case.get('rect'), corners=case.get('corners'),
                     raw=case.get('raw'), per_k={})
        for k in FACTORS:
            base = image(case, k)
            box, tex = base
            noise, kept_min, kept_exact, in_band = noise_and_distance(case, k, base)
            covered = [p for p, t in tex.items() if t is not None]
            kept = [p for p in covered if not noise[p]]
            info = dict(box=list(box), covered=len(covered), noise_covered=len(covered) - len(kept), denominator=len(kept),
                        noise_total=sum(1 for v in noise.values() if v), min_texel_distance=(float(kept_min) if kept_min is not None else None),
                        kept_exact_ties=kept_exact, kept_in_8bit_band=in_band)
            # R at k = 1
            if k == 1 and 'rect' not in case:
                rpx = redux.quad([(x, y) for x, y in case['corners']], case_uvs(case))
                diff = 0
                union = 0
                same_probe = set()
                for p in set(rpx) | set(covered):
                    rt = rpx.get(p)
                    bt = tex.get(p)
                    if rt is None and bt is None:
                        continue
                    union += 1
                    if rt != bt:
                        diff += 1
                    else:
                        same_probe.add(p)
                info['redux_union'] = union
                info['redux_differ'] = diff
                info['redux_differ_kept'] = sum(1 for p in set(rpx) | set(covered) if rpx.get(p) != tex.get(p) and not noise.get(p, False))
            else:
                same_probe = None
            # arrays
            w = box[2] - box[0]
            h = box[3] - box[1]
            codes = []
            nz = []
            for sy in range(box[1], box[3]):
                for sx in range(box[0], box[2]):
                    t = tex[(sx, sy)]
                    codes.append(-1 if t is None else t[0] * TEX + t[1])
                    nz.append(1 if noise[(sx, sy)] else 0)
            info['w'] = w
            info['h'] = h
            info['texel_codes'] = codes
            info['noise'] = nz
            # mutations
            muts = {}
            probe_candidates = []
            for p in kept:
                if margins_ok(case, k, *p) and (same_probe is None or p in same_probe):
                    probe_candidates.append(p)
            for mname, mut in MUTATIONS.items():
                if 'rect' in case:
                    continue
                if mname == 'no_mirror_recul' and not is_mirrored(case):
                    continue
                if mname == 'no_slope_term' and k == 1:
                    continue
                changed = []
                changed_noise = 0
                for p in probe_candidates:
                    if bk(case, k, p[0], p[1], mut) != tex[p]:
                        changed.append(p)
                diff_all = 0
                for p in covered:
                    if bk(case, k, p[0], p[1], mut) != tex[p]:
                        diff_all += 1
                muts[mname] = dict(differs_on_covered=diff_all, probes_changed=len(changed), first_probe=(list(changed[0]) if changed else None))
            info['mutations'] = muts
            info['probe_candidates'] = len(probe_candidates)
            info['_cand'] = probe_candidates
            entry['per_k'][str(k)] = info
        result['cases'].append(entry)
    return result


def pick_probes(result):
    """Probes: per case and factor, for each mutation the first probe that changes it, then a deterministic spread of up to 6."""
    by_name = {c['name']: c for c in CASES}
    for entry in result['cases']:
        case = by_name[entry['name']]
        for k in FACTORS:
            info = entry['per_k'][str(k)]
            cand = info.pop('_cand')
            chosen = []
            for mname, m in info['mutations'].items():
                if m['first_probe'] is not None and m['first_probe'] not in chosen:
                    chosen.append(m['first_probe'])
            step = max(1, len(cand) // 6)
            for p in cand[::step][:6]:
                if list(p) not in chosen:
                    chosen.append(list(p))
            probes = []
            for p in chosen:
                t = bk(case, k, p[0], p[1])
                probes.append(dict(x=p[0], y=p[1], texel=list(t), color=list(expected_color(case, t))))
            info['probes'] = probes


def row_equality(result):
    by_name = {c['name']: c for c in result['cases']}
    rows = []
    for a, b in ROW_PAIRS:
        for k in FACTORS:
            ia, ib = by_name[a]['per_k'][str(k)], by_name[b]['per_k'][str(k)]
            ca, cb = by_name[a]['per_k'][str(k)], by_name[b]['per_k'][str(k)]
            ba = [c for c in CASES if c['name'] == a][0]
            bb = [c for c in CASES if c['name'] == b][0]
            ax0, ay0 = ia['box'][0], ia['box'][1]
            bx0, by0 = ib['box'][0], ib['box'][1]
            # compare the texel codes of the two regions of the same size, pixel for pixel
            same = ia['w'] == ib['w'] and ia['h'] == ib['h'] and ia['texel_codes'] == ib['texel_codes']
            rows.append(dict(a=a, b=b, k=k, equal=same, covered=ia['covered']))
    result['rows'] = rows


def summary_md(result):
    lines = ['# G2b1-0 predictions (generated by g2b1_predict.py)', '',
             'Texture 16 x 16, texel (i, j) colour `texel_color(i, j)`, STP (alpha 128) where (i + j) % 4 == 1, texel (15, 15) = background (100, 150, 200).',
             'Rule B(k): coverage by the top-left corner of the screen pixel, texel floor(u(p) + 1/2), p = ((sx + 1/2)/k - 1/2, ...). Noise zone: texel within 2**-10 of a boundary (not exact ties), or corner within 2**-10 px of an edge or the diagonal (not exactly on it).', '',
             '| case | k | covered | noise (covered) | denominator | min texel distance kept | exact ties kept | kept in [2^-10, 2^-8) | R differs (union) | probes |',
             '|---|---:|---:|---:|---:|---:|---:|---:|---|---:|']
    for e in result['cases']:
        for k in FACTORS:
            i = e['per_k'][str(k)]
            r = f"{i['redux_differ']} ({i['redux_union']})" if 'redux_differ' in i else '-'
            md = i['min_texel_distance']
            lines.append(f"| {e['name']} | {k} | {i['covered']} | {i['noise_covered']} | {i['denominator']} | {md if md is None else format(md, '.6f')} | {i['kept_exact_ties']} | {i['kept_in_8bit_band']} | {r} | {len(i['probes'])} |")
    lines += ['', '## Mutations (pixels of B changed over covered pixels / probes that change)', '',
              '| case | k | ' + ' | '.join(MUTATIONS) + ' |', '|---|---:|' + '---:|' * len(MUTATIONS)]
    for e in result['cases']:
        for k in FACTORS:
            m = e['per_k'][str(k)]['mutations']
            cells = []
            for name in MUTATIONS:
                cells.append(f"{m[name]['differs_on_covered']} / {m[name]['probes_changed']}" if name in m else 'n/a')
            lines.append(f"| {e['name']} | {k} | " + ' | '.join(cells) + ' |')
    lines += ['', 'Mutation "texel chosen by the sampler instead of the shader": it can only change a kept pixel whose texel distance to a boundary lies in [2^-10, 2^-8) texel (8 sub-texel bits of Direct3D 11). Measured: the column "kept in [2^-10, 2^-8)" above is 0 for every case and factor (the smallest kept distance is the "min texel distance kept" column), so no probe can see that mutation; it is closed by design (the shader picks the texel, G2b1-R6), not by a threshold.']
    lines += ['', '## 1:1 rows: (a) quad equals (b) rectangle path, pixel for pixel (texel and coverage)', '',
              '| a | b | k | covered | equal |', '|---|---|---:|---:|---|']
    for r in result['rows']:
        lines.append(f"| {r['a']} | {r['b']} | {r['k']} | {r['covered']} | {r['equal']} |")
    return '\n'.join(lines) + '\n'


def main():
    result = build()
    pick_probes(result)
    row_equality(result)
    md = summary_md(result)
    if '--check' in sys.argv:
        print(md)
        return
    with open(os.path.join(HERE, 'g2b1_predictions.json'), 'w', newline='\n') as f:
        json.dump(result, f, separators=(',', ':'))
    with open(os.path.join(HERE, 'g2b1-predictions.md'), 'w', newline='\n') as f:
        f.write(md)
    print(md)


if __name__ == '__main__':
    main()
