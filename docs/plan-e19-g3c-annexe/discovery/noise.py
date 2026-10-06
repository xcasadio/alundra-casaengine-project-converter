"""Noise zone of an effect frame under B(k): pixels whose texel is within 2^-10 of a boundary (not an exact tie) or whose pixel corner is
within 2^-10 px of an edge/diagonal of a covering quad. Same definition as g2b1_predict.noise_and_distance."""
from fractions import Fraction as F
import numpy as np
import g2b1_predict as P

def noise_mask(quads, k, width=320, height=240):
    mask = np.zeros((height * k, width * k), dtype=bool)
    min_d = None; exact = 0; in_band = 0
    for q in quads:
        case = dict(corners=q["corners"], raw=q["raw"], mode=q["mode"])
        xs = [c[0] for c in q["corners"]]; ys = [c[1] for c in q["corners"]]
        x0, x1 = max(0, k * min(xs) - 1), min(width * k, k * max(xs) + 2)
        y0, y1 = max(0, k * min(ys) - 1), min(height * k, k * max(ys) + 2)
        for sy in range(y0, y1):
            for sx in range(x0, x1):
                if P.bk(case, k, sx, sy) is None:
                    continue
                n = False
                d2 = P.edge_distance2(case, k, sx, sy)
                if d2 is not None and 0 < d2 < P.NOISE * P.NOISE:
                    n = True
                pos = P.texel_position(case, k, sx, sy)
                if pos is not None and not n:
                    for d in (P.frac_distance(pos[0]), P.frac_distance(pos[1])):
                        if 0 < d < P.NOISE:
                            n = True
                        elif d == 0:
                            exact += 1
                        else:
                            min_d = d if min_d is None or d < min_d else min_d
                            if d < F(1, 256):
                                in_band += 1
                if n:
                    mask[sy, sx] = True
    return mask, (float(min_d) if min_d is not None else None), exact, in_band
