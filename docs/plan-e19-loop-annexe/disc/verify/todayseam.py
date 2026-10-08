"""Today's whole-file seam (what the DLL plays: last sample of the file -> sample 0) vs the track's own 99th percentile
sample-to-sample step (max over both channels), on the current data-extracted WAVs."""
import json, os, struct
ROOT = r"D:\development\repo\alundra-casaengine-project-converter\data-extracted\sound"
bj = json.load(open(os.path.join(ROOT, "bgm.json")))
n_click = 0
rows = []
for r in bj:
    b = open(os.path.join(ROOT, "bgm", r["File"]), "rb").read()
    n = (len(b) - 44) // 2
    s = struct.unpack_from("<%dh" % n, b, 44)
    L, R = s[0::2], s[1::2]
    steps = []
    stride = 7  # subsample for speed
    for ch in (L, R):
        steps += [abs(ch[i + 1] - ch[i]) for i in range(0, len(ch) - 1, stride)]
    steps.sort()
    p99 = steps[int(0.99 * (len(steps) - 1))]
    seam = max(abs(L[0] - L[-1]), abs(R[0] - R[-1]))
    rows.append((r["SoundIndex"], seam, p99, r["LoopDetected"]))
    if seam > p99:
        n_click += 1
for x in rows:
    print(x)
print("seam step > p99 on", n_click, "of", len(rows))
