"""obst.py - data of the obstacles the census names for the entity sites (flags, box, anim 0 Acceleration bit 0x80, position)."""
import sys

sys.stdout.reconfigure(encoding="utf-8")
sys.path.insert(0, ".")
import mysim as S  # noqa: E402
from compare import mapdata  # noqa: E402

CASES = [(185, 6), (185, 7), (61, 32), (61, 33), (61, 34), (61, 35), (62, 19), (62, 20), (62, 21), (68, 21), (52, 34), (52, 36),
         (199, 4), (346, 7), (346, 10), (426, 46), (10, 41), (10, 19), (10, 18), (10, 21), (240, 6)]
for mid, rid in CASES:
    F, recs = mapdata(mid)
    r = recs.get(rid)
    if r is None:
        print(mid, rid, "missing")
        continue
    h = S.SPR.get(r.get("PrefabAssetId"))
    if h is None:
        print(mid, rid, r.get("EntityName"), "no header")
        continue
    fl = S.flags_of(h)
    a0 = [a for a in h["AnimSets"] if a["Anim"] == 0]
    print("%4d rec%-3d %-34s flags 0x%06X collidable=%d grav=%d box(%d,%d;%d,%d;z%d,%d) anim0.acc=0x%s pos=(%d,%d) h=%s dir=0x%X C=%s ctrl=%s" % (
        mid, rid, r.get("EntityName")[:34], fl, bool(fl & 0x80), bool(fl & 0x100), h["OffsetX"], h["OffsetY"], h["SizeX"], h["SizeY"],
        h.get("OffsetZ", 0), h.get("SizeZ", 0), ("%X" % a0[0]["Acceleration"]) if a0 else "-", int(r["XPos"]) * 12 + 12, int(r["YPos"]) * 8 + 8,
        r.get("Height"), int(r.get("SpriteDirection", 0)), r.get("EventCodesC_TickIndex"), S.has_controller(r.get("PrefabAssetId"))))
h = S.SPR[S.HERO]
print("hero flags 0x%06X box(%d,%d;%d,%d)" % (S.flags_of(h), h["OffsetX"], h["OffsetY"], h["SizeX"], h["SizeY"]))
