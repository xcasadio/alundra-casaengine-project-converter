import json
d = json.load(open(r"C:\Users\casad\AppData\Local\Temp\claude\D--development-repo-alundra-casaengine-project-converter\b00d1a72-420d-4acc-ab34-0c25fbdad3a1\scratchpad\loop-data\oracle\sound\bgm.json", encoding="utf-8-sig"))
B = 16384; BA = 4; SR = 44100
rows = []
for e in d:
    if not e["Looping"]: continue
    D = e["Frames"] * 735 * BA
    L = D - e["LoopStartSample"] * BA
    short = L % B
    # first pass short buffer (from 0): D % B ; loop passes: L % B
    rows.append((short/BA/SR*1000, e["SoundIndex"], short, L // B))
rows.sort()
print("smallest loop-pass seam buffers (ms, track, bytes, full buffers):")
for r in rows[:6]: print(r)
m = rows[0]
print("worst margin approx near seam (ms) with target 3:", 92.88 + m[0], "; best-case non-seam worst", 2*92.88)
print("depth 24 criteria: 23 buffers", 23*92.88, " 22 buffers+short", 22*92.88 + m[0])
