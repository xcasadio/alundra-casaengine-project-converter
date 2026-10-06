"""Best-case float32 evaluation of the shader texel coordinate at EXACT texel ties on a tall sheet: v normalized with the CPU shift (0.5 + 1/4096),
interpolated with float32 barycentric weights (correctly rounded), multiplied by the texture height in float32, then floor. Counts ties resolved the wrong way."""
import random
import numpy as np
from fractions import Fraction as F
f32 = np.float32
random.seed(3)
for H in (435, 1105):
    wrong = 0; ties = 0; trials = 0
    for _ in range(4000):
        # triangle with integer PS1 vertices, window of 40 texels at a random row near the bottom of the sheet
        V0 = random.randint(H - 120, H - 60); w = h = random.choice([24, 32, 48])
        tri = [(random.randint(0, 60), random.randint(0, 60)) for _ in range(3)]
        uv = [(0, V0), (w, V0), (0, V0 + h)]
        (x0, y0), (x1, y1), (x2, y2) = tri
        det = (x1 - x0) * (y2 - y0) - (x2 - x0) * (y1 - y0)
        if det == 0: continue
        trials += 1
        for sy in range(0, 62):
            for sx in range(0, 62):
                l1 = F((sx - x0) * (y2 - y0) - (x2 - x0) * (sy - y0), det)
                l2 = F((x1 - x0) * (sy - y0) - (sx - x0) * (y1 - y0), det)
                l0 = 1 - l1 - l2
                if min(l0, l1, l2) < 0: continue
                v = l0 * uv[0][1] + l1 * uv[1][1] + l2 * uv[2][1]          # exact v at the integer sample point (k = 1: p = (sx, sy))
                t = v + F(1, 2)
                if t.denominator != 1: continue                             # keep only exact ties: u + 1/2 integer
                ties += 1
                # shader: normalized attributes with the CPU shift, interpolated in float32, times H
                a = [f32((F(vv) + F(1, 2) + F(1, 4096)) / H) for vv in (uv[0][1], uv[1][1], uv[2][1])]
                val = f32(f32(a[0] * f32(float(l0))) + f32(a[1] * f32(float(l1)))) + f32(a[2] * f32(float(l2)))
                tt = f32(val * f32(H))
                if int(np.floor(tt)) != int(t):                             # expected texel index = floor(v + 1/2) = t (exact tie resolved upward)
                    wrong += 1
    print("height", H, "triangles", trials, "exact tie samples", ties, "float32 floor resolved wrongly", wrong, "(%.2f%%)" % (100 * wrong / max(1, ties)))
