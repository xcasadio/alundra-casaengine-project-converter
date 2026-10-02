"""cloud.py - obstacle cloud for the ORIG / FIX runs of c0b.py: where every Collidable record can REST.

Source: walks_<TAG>.pkl of a c0b.py run (states per pc per root program). A record is put at a pin when, at a suspension
op of a program, it is that program's owner or its logic entity, it is pinned, and its current animation has speed 0
(it stands). The hero is put at its pinned positions in its own map-event programs while the program holds the control.
Load-spawned records with no script C program stand at their record position. Only records whose sprite header is
Collidable (Flags & 0x80) and whose standing animation has AnimFlags (anim set byte 0xD) bit 0x80 clear are kept.
Over-approximation by design: a point says "can stand there at some time", never "is there at the time of a given walk".
Usage: python cloud.py [TAG ...]   -> cloud.pkl (union of the given runs; default DLL)
"""
import collections
import os
import pickle
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
from census_exec_orig import Map, SZ, HERO, header, anim_speed, rec_pos, HERO_ID  # noqa: E402

sys.stdout.reconfigure(encoding="utf-8")


class BPin(tuple):
    """unpickling stub of c0b.BPin"""
RESTING = {0x00, 0x36, 0x37, 0x39, 0x0D, 0x5C, 0xC4, 0x9F, 0x1C, 0x1D, 0xFF, 0x2C, 0x3B, 0x50, 0x51, 0x44, 0x6E, 0x70, 0x2F}


def hflags(h):
    return h["MoreFlags"] | (h["CanPickup"] << 8) | (h["FlagsPortraitShadowType"] << 16)


def af80(pid, anim):
    h = header(pid)
    for a in h["AnimSets"]:
        if a["Anim"] == anim:
            return bool(a["Acceleration"] & 0x80)
    return False


def build(tags):
    cloud = {}
    for tag in tags:
        walks = pickle.load(open(HERE + "/walks_%s.pkl" % tag, "rb"))
        for mid, wk in walks.items():
            mp = Map(mid)
            recs = {int(r["Index"]): r for r in mp.records}
            owners = wk["__owners__"]
            pts = cloud.setdefault(mid, {}).setdefault("points", set())
            scriptc = {o for lab, o in owners.items() if " C[" in lab}
            for lab, states in wk.items():
                if lab == "__owners__":
                    continue
                owner = owners[lab]
                for pc, sts in states.items():
                    op = mp.C[pc]
                    if op in (0x2D, 0x8A, 0x8B):
                        # a record spawned here that has no script C program stands where it is spawned (static blocks)
                        v = mp.C[pc:pc + 10] + [0] * 10
                        rid = v[2] if op == 0x8B else v[1]
                        if rid in recs and ("R", rid) not in scriptc:
                            pid = recs[rid].get("PrefabAssetId")
                            h = header(pid)
                            if h is not None and (hflags(h) & 0x80) and not af80(pid, 0):
                                for (logic, actors, lock) in sts:
                                    if op == 0x8A:
                                        p = (v[2] | (v[3] << 8), v[4] | (v[5] << 8))
                                    elif op == 0x2D:
                                        p = rec_pos(recs[rid])
                                    else:
                                        sel = v[1]
                                        ref = logic if sel == 0x80 else (HERO if sel == 0x81 else (("R", sel) if sel < 0x80 else None))
                                        rp = None
                                        for a in actors:
                                            if a[0] == ref:
                                                rp = a[3]
                                        if rp is None:
                                            continue
                                        s16 = lambda lo, hi: ((lo | (hi << 8)) ^ 0x8000) - 0x8000
                                        p = (rp[0] + s16(v[3], v[4]), rp[1] + s16(v[5], v[6]))
                                    pts.add((("R", rid), float(p[0]), float(p[1]), "spawn@%d %s" % (pc, lab)))
                    if op not in RESTING:
                        continue
                    for (logic, actors, lock) in sts:
                        for (key, anim, d, pin, mv) in actors:
                            if pin is None:
                                continue
                            if key == HERO:
                                if owner == HERO and lock:
                                    pts.add((HERO, float(pin[0]), float(pin[1]), "hero@%d" % pc))
                                continue
                            if not (isinstance(key, tuple) and key[0] == "R") or key[1] not in recs:
                                continue
                            if key != owner and key != logic:
                                continue
                            pid = recs[key[1]].get("PrefabAssetId")
                            h = header(pid)
                            if h is None or not (hflags(h) & 0x80):
                                continue
                            an = anim if anim is not None else 0
                            if anim_speed(pid, an):
                                continue
                            if af80(pid, an):
                                continue
                            pts.add((key, float(pin[0]), float(pin[1]), "rest@%d %s" % (pc, lab)))
            for i, r in recs.items():
                if int(r.get("IsEnabled", 1)) != 0 and (int(r.get("SpriteDirection", 0)) & 0x40) and ("R", i) not in scriptc:
                    pid = r.get("PrefabAssetId")
                    h = header(pid)
                    if h is None or not (hflags(h) & 0x80) or af80(pid, 0):
                        continue
                    p = rec_pos(r)
                    pts.add((("R", i), float(p[0]), float(p[1]), "load"))
    for mid in cloud:
        # collapse: one point per (key, x, y), sources joined
        agg = collections.defaultdict(list)
        for key, x, y, src in cloud[mid]["points"]:
            agg[(key, round(x, 2), round(y, 2))].append(src)
        cloud[mid]["points"] = [(k[0], k[1], k[2], ";".join(sorted(set(v))[:4])) for k, v in sorted(agg.items(), key=lambda t: repr(t[0]))]
    return cloud


if __name__ == "__main__":
    tags = sys.argv[1:] or ["DLL"]
    c = build(tags)
    pickle.dump(c, open(HERE + "/cloud.pkl", "wb"))
    n = sum(len(v["points"]) for v in c.values())
    print("maps", len(c), "points", n)
    for mid in (10, 185, 179, 164, 178, 135, 331, 363, 365, 366, 334):
        if mid in c:
            print(mid, len(c[mid]["points"]), [p for p in c[mid]["points"] if p[0] != HERO][:60])
