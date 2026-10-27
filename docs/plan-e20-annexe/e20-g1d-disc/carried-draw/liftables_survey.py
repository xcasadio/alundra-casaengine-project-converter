"""Read-only: every sprite record that the hero can lift (pickup kind != 0), with the collidable bit and the IDSV of anim 0 (frame 0 per direction).
Hero carry animations (alundra_0) for comparison: see idsv_dump.py. Output: one line per record."""
import json
from collections import Counter

P = r"D:/development/repo/alundra-casaengine-project-converter/alundra-project/Data/sprite-records.json"
d = json.load(open(P, encoding="utf-8"))

rows = []
for gid, r in d.items():
    flags = r["MoreFlags"] | (r["CanPickup"] << 8) | (r["FlagsPortraitShadowType"] << 16)
    kind = (flags & 0x600) >> 9
    if kind == 0:
        continue
    a0 = [e for e in r["IdsvAnimDirs"] if e["Anim"] == 0]
    idsv0 = {e["Direction"]: e["Frames"][0] for e in a0} if a0 else {}
    rows.append((r["BankKey"], kind, bool(flags & 0x80), idsv0, r["SizeZ"]))

rows.sort(key=lambda x: (x[0].startswith("alundra_") is False, x[0]))
print("BankKey, pickupKind, collidable, IDSV(anim0, frame0, per direction), SizeZ")
for bk, kind, col, idsv0, sz in rows:
    print(f"{bk:14s} kind={kind} collidable={col!s:5s} idsv0={idsv0} sizeZ={sz}")
print()
print("liftable records:", len(rows), " collidable:", sum(1 for r in rows if r[2]), " not collidable:", sum(1 for r in rows if not r[2]))
c = Counter(max(r[3].values()) if r[3] else None for r in rows)
print("max IDSV of anim 0 among the liftables:", sorted(c.items(), key=lambda kv: (kv[0] is None, kv[0])))
