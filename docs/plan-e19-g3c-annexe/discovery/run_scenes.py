import time, numpy as np
import g3c_model as M, noise as N
BG = (24, 40, 56)

def scene(name, mapid, slots, scroll, ks=(1,)):
    """slots: list of (table, anim, frame, pos, slot_index)"""
    doc, sheet = M.load_companion(mapid); sheets = {"map": sheet}
    quads = []
    for (t, a, f, pos, idx) in slots:
        quads += M.slot_quads(doc, t, a, f, pos, idx, scroll, "map")
    quads.sort(key=lambda q: q["sortkey"])
    for k in ks:
        t0 = time.time()
        img, cov = M.render(quads, sheets, k, bg=BG)
        mask, min_d, exact, band = N.noise_mask(quads, k)
        diff = int((img != np.array(BG)).any(axis=2).sum())
        print(name, "k", k, "slots", len(slots), "quads", len(quads), "covered per quad", cov, "pixels != bg", diff, "noise", int(mask.sum()),
              "sha1", M.digest(img)[:16], "%.0fs" % (time.time() - t0), flush=True)
        M.save_png(img, "ref_%s_k%d.png" % (name, k))
    return quads

doc163, _ = M.load_companion(163)
slots = []
for i, rec in enumerate(doc163["Records"]):
    slots.append((rec["Effect"], rec["Anim"], 0, M.record_position(rec), i))
q = scene("163_rays_tick0", 163, slots, (220, 40), ks=(1, 3))
print("163 anchors", [x["anchor"] for x in q if x["image"] == 0], "elev", {x["sortkey"][0] for x in q})

doc391, _ = M.load_companion(391)
slots = []
for i, rec in enumerate(doc391["Records"][:2]):
    slots.append((rec["Effect"], rec["Anim"], 0, M.record_position(rec), i))
q = scene("391_pieces_01_tick0", 391, slots, (836, 400), ks=(1, 3))
print("391 anchors", [x["anchor"] for x in q if x["image"] == 0], "elev", {x["sortkey"][0] for x in q})
