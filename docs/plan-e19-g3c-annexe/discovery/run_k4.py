import time, numpy as np
import g3c_model as M, noise as N
BG = (24, 40, 56)
doc, sheet = M.load_companion(476)
pos = (960 << 16, 160 << 16, (48 << 16) + 1)
quads = M.slot_quads(doc, 0, 1, 0, pos, 0, (800, -8), "map"); quads.sort(key=lambda q: q["sortkey"])
t0 = time.time()
img, cov = M.render(quads, {"map": sheet}, 4, bg=BG)
mask, min_d, exact, band = N.noise_mask(quads, 4)
print("k4 aura tick0 pixels not bg", int((img != np.array(BG)).any(axis=2).sum()), "noise", int(mask.sum()), "exact ties", exact, "band", band, "sha1", M.digest(img), "%.0fs" % (time.time() - t0))
# deviation of B(4) against 4x nearest-neighbour magnification of B(1) on the same background
