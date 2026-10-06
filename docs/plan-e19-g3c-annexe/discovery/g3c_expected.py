"""Reference frames of the G3c pixel proof (B(k), exact rationals) for the three scenes, on the flat background BG, with the noise zone,
the digests and a probe set (k = 1: pixels where B equals the hardware walker R, margins >= 1/16 texel and 1/16 px, not noise).
Usage: python g3c_expected.py [k ...]   (default 1 3).  Writes g3c_expected.json and ref_<scene>_k<k>.png"""
import json, sys, time
import numpy as np
import g3c_model as M, noise as N, redux_nomask as R
import g2b1_predict as P
BG = (24, 40, 56)
KS = [int(a) for a in sys.argv[1:]] or [1, 3]

def scenes():
    out = {}
    d476, s476 = M.load_companion(476)
    pos = (960 << 16, 160 << 16, (48 << 16) + 1)                     # the real 0xA2 [0, 960, 160, 48]
    for t, frame in ((0, 0), (2, 1), (16, 8)):
        out["476_aura_tick%d" % t] = dict(doc=d476, sheet=s476, scroll=(800, -8), slots=[(0, 1, frame, pos, 0)], note="record 0, animation 1, slot tick %d (frame %d)" % (t, frame))
    d163, s163 = M.load_companion(163)
    out["163_rays_tick0"] = dict(doc=d163, sheet=s163, scroll=(220, 40), slots=[(r["Effect"], r["Anim"], 0, M.record_position(r), i) for i, r in enumerate(d163["Records"])], note="4 load-time rays, ticks 0..10")
    d391, s391 = M.load_companion(391)
    out["391_pieces_tick0"] = dict(doc=d391, sheet=s391, scroll=(836, 640), slots=[(r["Effect"], r["Anim"], 0, M.record_position(r), i) for i, r in enumerate(d391["Records"])], note="5 load-time pieces (slots 1-4 visible)")
    return out

def redux_image(quads, sheet, width=320, height=240):
    img = np.zeros((height, width, 3), dtype=np.int32); img[:, :] = BG
    sh, sw = sheet.shape[:2]
    for q in quads:
        sx0, sy0, w, h = q["raw"]
        px = R.quad(q["corners"], [(0, 0), (w, 0), (0, h), (w, h)])
        for (x, y), (tu, tv) in px.items():
            if not (0 <= x < width and 0 <= y < height): continue
            r, g, b, a = (int(c) for c in sheet[min(max(sy0 + tv, 0), sh - 1), min(max(sx0 + tu, 0), sw - 1)])
            if a == 0: continue
            img[y, x] = (r, g, b) if (q["mode"] is None or a == 255) else M.blend(tuple(int(c) for c in img[y, x]), (r, g, b), q["mode"])
    return img

result = {}
for name, sc in scenes().items():
    quads = []
    for (t, a, f, pos, idx) in sc["slots"]:
        quads += M.slot_quads(sc["doc"], t, a, f, pos, idx, sc["scroll"], "map")
    # the cull of the anchor (the whole slot is dropped)
    quads = [q for q in quads if -128 <= q["anchor"][0] - sc["scroll"][0] <= 448 and -128 <= q["anchor"][1] - sc["scroll"][1] <= 368]
    quads.sort(key=lambda q: q["sortkey"])
    entry = dict(note=sc["note"], scroll=sc["scroll"], quads=len(quads), order=[(q["sortkey"][3], q["image"]) for q in quads], per_k={})
    for k in KS:
        t0 = time.time()
        img, cov = M.render(quads, {"map": sc["sheet"]}, k, bg=BG)
        mask, min_d, exact, band = N.noise_mask(quads, k)
        info = dict(covered_per_quad=cov, pixels_not_bg=int((img != np.array(BG)).any(axis=2).sum()), noise_pixels=int(mask.sum()),
                    min_texel_distance_kept=min_d, exact_ties_kept=exact, band_2e10_2e8=band, sha1_rgb=M.digest(img))
        M.save_png(img, "ref_%s_k%d.png" % (name, k))
        if k == 1:
            rimg = redux_image(quads, sc["sheet"])
            diff = (img != rimg).any(axis=2)
            lit = ((img != np.array(BG)).any(axis=2) | (rimg != np.array(BG)).any(axis=2))
            info["B_vs_R_differ"] = int(diff.sum()); info["B_or_R_lit"] = int(lit.sum())
            # probes: B == R, not noise, every covering quad has margins >= 1/16
            good = ~diff & ~mask & lit
            for q in quads:
                case = dict(corners=q["corners"], raw=q["raw"], mode=q["mode"])
                xs = [c[0] for c in q["corners"]]; ys = [c[1] for c in q["corners"]]
                for sy in range(max(0, min(ys) - 1), min(240, max(ys) + 2)):
                    for sx in range(max(0, min(xs) - 1), min(320, max(xs) + 2)):
                        if good[sy, sx] and P.bk(case, 1, sx, sy) is not None and not P.margins_ok(case, 1, sx, sy):
                            good[sy, sx] = False
            ys_, xs_ = np.nonzero(good)
            pts = list(zip(xs_.tolist(), ys_.tolist()))
            step = max(1, len(pts) // 8)
            info["probes_k1"] = [dict(x=x, y=y, rgb=[int(c) for c in img[y, x]]) for (x, y) in pts[::step][:8]]
            info["probe_candidates"] = len(pts)
        entry["per_k"][str(k)] = info
        print(name, "k", k, {kk: vv for kk, vv in info.items() if kk != "probes_k1"}, "%.0fs" % (time.time() - t0), flush=True)
    result[name] = entry
json.dump(result, open("g3c_expected.json", "w"), indent=1)
