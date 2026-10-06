import numpy as np
from assets import *
def tdiv(a, b):  # C truncation toward zero
    return int(a / b) if a * b >= 0 else -int(-a / b)
STEPS = 15
def flight_sizes(h, ret):
    out = []
    for c in range(STEPS, 0, -1):
        k = c if ret else STEPS - c
        w_ = 48 * k // STEPS; h_ = h * k // STEPS
        rgb = 127 + ((STEPS - c) * 128 // STEPS if ret else (c * 128) // STEPS)
        out.append((c, w_, h_, rgb))
    return out
def psx_mod(px, rgb):
    """5-bit modulation model: out5 = min(31, t5 * rgb // 128); input exported 8-bit = (t5 << 3) | (t5 >> 2)."""
    t5 = (px[..., :3].astype(np.int32) >> 3)
    o5 = np.minimum(31, (t5 * rgb) // 128)
    o8 = (o5 << 3) | (o5 >> 2)
    return o8, (o5 << 3) | (o5 >> 2)
def sample_left(img, w, h):
    """PSX FT4 rule [hyp, public GPU docs]: texel = floor(i * src / dst) (no half-pixel offset)."""
    H, W = img.shape[:2]
    ys = (np.arange(h) * H) // h; xs = (np.arange(w) * W) // w
    return img[np.ix_(ys, xs)]
def sample_center(img, w, h):
    """GPU nearest at pixel centres: texel = floor((i + 0.5) * src / dst)."""
    H, W = img.shape[:2]
    ys = np.floor((np.arange(h) + 0.5) * H / h).astype(int); xs = np.floor((np.arange(w) + 0.5) * W / w).astype(int)
    return img[np.ix_(ys, xs)]
if __name__ == '__main__':
    tot_area = 0; tot_changed = {1: 0, 8: 0, 24: 0}
    per_pass = {}
    for p in portraits():
        px = sprite_pixels(p['id'])
        assert px.shape[:2] == (p['h'], p['w']), (p, px.shape)
        for ret in (False, True):
            for c, w_, h_, rgb in flight_sizes(p['h'], ret):
                if w_ == 0 or h_ == 0: continue
                s = sample_left(px, w_, h_)
                opaque = s[..., 3] > 0
                o8, _ = psx_mod(s, rgb)
                d = np.abs(o8 - s[..., :3].astype(np.int32)).max(axis=2)
                n = int(opaque.sum())
                key = ('out' if ret else 'in', c, rgb)
                acc = per_pass.setdefault(key, [0, 0, 0, 0, 0.0, 0])
                acc[0] += n
                acc[1] += int(((d >= 1) & opaque).sum()); acc[2] += int(((d >= 8) & opaque).sum()); acc[3] += int(((d >= 24) & opaque).sum())
                acc[4] += float(d[opaque].mean()) if n else 0.0; acc[5] += 1
    print('mean over the 25 portraits, per pass (sizes for h=56): direction, c, rgb : opaque px | share changed >=1 level | >=8 | >=24 | mean max-channel delta')
    for key in sorted(per_pass, key=lambda k: (k[0], -k[1])):
        n, a, b, cc, md, k = per_pass[key]
        print(key, 'opaque %d' % (n // 25), 'chg>=1 %.0f%%' % (100 * a / n), '>=8 %.0f%%' % (100 * b / n), '>=24 %.0f%%' % (100 * cc / n), 'meanmax %.1f' % (md / k))
