"""census1.py - read-only census of the sprite types by the flags that decide the carrier/passenger roles (binary: 0x800364C8 subject = (flags & 0x4100) == 0x100,
candidate = any other member of the collidable list = flags & 0x80). Writes census1.json next to itself and prints a summary.
"""
import sys, os, json, collections, glob, re
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(HERE, "annex", "work", "h2tools"))
import m
sys.stdout.reconfigure(encoding="utf-8")

SPR = json.load(open(m.ROOT + "/alundra-project/Data/sprite-records.json", encoding="utf-8-sig"))


def all_maps():
    out = []
    for f in glob.glob(m.MAPS + "/*/*/events/*.events.json"):
        base = os.path.basename(os.path.dirname(os.path.dirname(f)))
        mm = re.search(r"-(\d+)$", base)
        if mm:
            out.append(int(mm.group(1)))
    return sorted(set(out))


def flags_of(h):
    return h["MoreFlags"] | (h["CanPickup"] << 8) | (h["FlagsPortraitShadowType"] << 16)


def main():
    types = {}      # pid -> dict(name, flags, size, off, records:[(map, idx)], speeds)
    maps = all_maps()
    nrec = 0
    nspawn = 0
    for mid in maps:
        mp = m.Map(mid)
        for r in mp.records:
            nrec += 1
            pid = r.get("PrefabAssetId")
            h = SPR.get(pid) if pid else None
            if not h:
                continue
            spawned = int(r.get("IsEnabled", 1)) != 0 and (int(r.get("SpriteDirection", 0)) & 0x40) != 0
            t = types.setdefault(pid, dict(name=r.get("EntityName"), flags=flags_of(h), size=(h["SizeX"], h["SizeY"], h["SizeZ"]),
                                           off=(h["OffsetX"], h["OffsetY"], h["OffsetZ"]),
                                           maxspeed=max([a["Speed"] for a in h["AnimSets"]] or [0]),
                                           records=[], spawned=0))
            t["records"].append((mid, int(r["Index"]), spawned))
            if spawned:
                t["spawned"] += 1
                nspawn += 1
    print("maps", len(maps), "records", nrec, "spawned-at-load", nspawn, "types used", len(types))
    json.dump({k: v for k, v in types.items()}, open(os.path.join(HERE, "census1.json"), "w", encoding="utf-8"), indent=1)

    # roles
    def role(f):
        coll = bool(f & 0x80)
        grav = bool(f & 0x100)
        noride = bool(f & 0x4000)
        return coll, grav, noride
    tot = collections.Counter()
    for pid, t in types.items():
        coll, grav, noride = role(t["flags"])
        key = ("collidable" if coll else "non-collidable", "gravity" if grav else "no-gravity", "noriders" if noride else "riders-ok")
        t["role"] = key
        tot[key] += len(t["records"])
    print("records by role:")
    for k, v in sorted(tot.items(), key=lambda kv: -kv[1]):
        print("  ", k, v)

    print()
    print("PASSENGER-CAPABLE types (collidable + gravity + not NoRiders): flags,size,names,records,maps")
    pas = [(pid, t) for pid, t in types.items() if t["role"] == ("collidable", "gravity", "riders-ok")]
    for pid, t in sorted(pas, key=lambda kv: -len(kv[1]["records"])):
        mapset = sorted(set(r[0] for r in t["records"]))
        print("  %-38s flags=%06x size=%s speedmax=%-4d records=%-4d spawned=%-4d maps=%d" % (t["name"], t["flags"], t["size"], t["maxspeed"], len(t["records"]), t["spawned"], len(mapset)))
    print("total passenger-capable records", sum(len(t["records"]) for _, t in pas), "types", len(pas))


if __name__ == "__main__":
    main()
