import sys, time, numpy as np
import g3c_model as M, noise as N
doc, sheet = M.load_companion(476); sheets = {"map": sheet}
pos = (960 << 16, 160 << 16, (48 << 16) + 1); scroll = (800, -8)
for k in (1, 3):
    for frame in (0, 1, 8):
        t = time.time()
        quads = M.slot_quads(doc, 0, 1, frame, pos, 0, scroll, "map"); quads.sort(key=lambda q: q["sortkey"])
        img, cov = M.render(quads, sheets, k)
        mask, min_d, exact, band = N.noise_mask(quads, k)
        covered = int((img.sum(axis=2) > 0).sum())
        print("k", k, "frame", frame, "nonblack", covered, "noise px", int(mask.sum()), "min texel dist kept %.6f" % (min_d or -1), "exact ties", exact, "in [2^-10,2^-8) band", band,
              "sha1(RGB)", M.digest(img)[:16], "%.0fs" % (time.time() - t), flush=True)
        np.save("ref_476_a1_f%d_k%d.npy" % (frame, k), img.astype(np.uint8)); np.save("noise_476_a1_f%d_k%d.npy" % (frame, k), mask)
