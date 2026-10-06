import numpy as np
from assets import *
from tint import *
tot = {}
for p in portraits():
    px = sprite_pixels(p['id'])
    for c, w_, h_, rgb in flight_sizes(p['h'], False):
        if w_ == 0 or h_ == 0: continue
        a = sample_left(px, w_, h_); b = sample_center(px, w_, h_)
        diff = np.any(a != b, axis=2)
        n = a.shape[0]*a.shape[1]
        acc = tot.setdefault((w_, h_), [0, 0, 0])
        acc[0] += int(diff.sum()); acc[1] += n; acc[2] += 1
print('size (w,h): share of dst pixels whose texel differs between the PSX rule (floor(i*src/dst)) and the GPU centre rule, mean over the 25 portraits')
for k in sorted(tot):
    d, n, m = tot[k]
    print(k, '%.0f%%' % (100*d/n))
# ambiguous (exact boundary) columns for the centre rule: (2i+1)*src/(2*dst) integer
print('widths with exact-boundary columns for the 48-wide source:')
for w_ in sorted({k[0] for k in tot}):
    n = sum(1 for i in range(w_) if ((2*i+1)*48) % (2*w_) == 0)
    print(w_, n)
