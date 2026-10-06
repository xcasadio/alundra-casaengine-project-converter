import sys
from fractions import Fraction as F
import my_render as M
name, px, py = sys.argv[1], int(sys.argv[2]), int(sys.argv[3])
doc, sheet, scroll, slots = M.SCENES[name]
quads = M.quads_of(slots, doc, scroll)
for q in quads:
    tl, tr, bl, br = q["corners"]
    for tri in ((tl, tr, bl), (tr, bl, br)):
        vals = []
        for i in range(3):
            a, b = tri[i], tri[(i+1)%3]
            vals.append((b[0]-a[0])*(py-a[1]) - (b[1]-a[1])*(px-a[0]))
        ymin = min(c[1] for c in tri)
        if ymin <= py <= max(c[1] for c in tri) + 1 and min(c[0] for c in tri) - 1 <= px <= max(c[0] for c in tri) + 1:
            print("image", q["image"], "tri", tri, "edge values at the pixel", vals, "inside mine", M.inside(tri, (F(px), F(py))))
