"""census2.py - read-only census, per map, of
  (a) the support graph of the records AT SPAWN (the binary's rider rule 0x800364C8 / DLL EntitySupport.FindRidingEntity): who rests on whom, chains of depth >= 2 (piles);
  (b) the carriers that move: Z (the 0x1B targets of the annex census_z.json) and XY (platform-class records whose own programs run a walking opcode or set a moving animation);
  (c) the passenger-capable records (collidable + Gravity, not NoRiders) of the maps that have a moving carrier.
Method for the programs: annex/work/census_z.py walk (all branches open, logic entity tracked by 0x42/0x43).
Output: census2.json + stdout.
"""
import sys, os, json, collections, glob, re
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(HERE, "annex", "work", "h2tools"))
sys.path.insert(0, os.path.join(HERE, "annex", "work"))
import m
import census_z
sys.stdout.reconfigure(encoding="utf-8")

SPR = census_z.SPR
SLOTS = census_z.SLOTS
HERO = census_z.HERO
WALK_OPS = {0x0B, 0x1E, 0x1F, 0x24}
ANIM_OPS = {0x1A, 0x59, 0x5B}
PLATFORM_PREFIX = ("Plateforme", "asiba", "Bulle", "Bloc transparent", "Gros bloc transparent", "Trappe", "Grille de fer", "Bois flottant", "Pilier poussable",
                   "Bloc à pousser", "Interrupteur (pi", "Porte (grande)", "Statue d")


def flags_of(h):
    return h["MoreFlags"] | (h["CanPickup"] << 8) | (h["FlagsPortraitShadowType"] << 16)


def spawned(r):
    return int(r.get("IsEnabled", 1)) != 0 and (int(r.get("SpriteDirection", 0)) & 0x40) != 0


class Ent:
    pass


def make_entities(mp):
    out = []
    for r in mp.records:
        pid = r.get("PrefabAssetId")
        h = SPR.get(pid) if pid else None
        if not h:
            continue
        e = Ent()
        e.idx = int(r["Index"])
        e.name = r.get("EntityName") or ""
        e.spawned = spawned(r)
        e.flags = flags_of(h)
        e.size = (h["SizeX"], h["SizeY"], h["SizeZ"])
        e.speeds = {a["Anim"]: a["Speed"] for a in h["AnimSets"]}
        px = int(r["XPos"]) * 12 + 12
        py = int(r["YPos"]) * 8 + 8
        pz = int(r["Height"]) * 8
        e.modx = (px + h["OffsetX"]) << 16
        e.mody = (py + h["OffsetY"]) << 16
        e.modz = (pz) << 16            # record Height << 19 = ModdedPosZ (the mapper's PosZ before the -ModZ adjustment)
        e.w = (e.size[0] << 16) - 1 if e.size[0] else 0
        e.h = (e.size[1] << 16) - 1 if e.size[1] else 0
        e.d = (e.size[2] << 16) - 1 if e.size[2] else 0
        e.collidable = bool(e.flags & 0x80)
        e.subject = (e.flags & 0x4180) == 0x180
        out.append(e)
    return out


def rests_on(s, o):
    """the DLL's FindRidingEntity (== the binary 0x800364C8) for subject s on candidate o"""
    if o is s:
        return False
    if o.modz + o.d + 1 != s.modz:
        return False
    dx = o.modx - s.modx
    if dx < 0:
        if not (s.modx - o.modx < o.w + 1):
            return False
    elif not (dx < s.w + 1 + 0):   # entityWidth = Width + 1
        return False
    dy = o.mody - s.mody
    if dy < 0:
        if not (s.mody - o.mody < o.h + 1):
            return False
    elif not (dy < s.h + 1):
        return False
    return True


def roots_of(mp):
    roots = []
    for e in mp.mapevents:
        idx = int(e["EventCodesBIndex"])
        t = mp.T["B"]
        if (idx & 0x7F) and (idx & 0x7F) < len(t) and t[idx & 0x7F]:
            roots.append(("map B[%d]" % (idx & 0x7F), t[idx & 0x7F], HERO, None))
    for r in mp.records:
        for slot, key in SLOTS:
            idx = int(r.get(key, 0))
            t = mp.T[slot]
            if (idx & 0x7F) and (idx & 0x7F) < len(t) and t[idx & 0x7F]:
                roots.append(("rec%s %s[%d]" % (r["Index"], slot, idx & 0x7F), t[idx & 0x7F], ("R", int(r["Index"])), int(r["Index"])))
    return roots


def main():
    maps = census_z.all_maps()
    zjson = json.load(open(os.path.join(HERE, "annex", "work", "census_z.json"), encoding="utf-8"))
    zmov = set()           # (map, rec) with a primary 0x1B on a collidable sized target
    for mid, e in zjson.items():
        for n, t in e["fly_targets"].items():
            sites = [x for x in t["sites"] if x["kind"] == "primary" and x["force"] != 0]
            p = t["prefab"]
            if sites and p and (p["flags"] & 0x80) and all(v > 0 for v in p["size"]):
                zmov.add((int(mid), int(n)))
    report = {}
    xy_total = 0
    piles = []
    onZ = []
    for mid in maps:
        mp = m.Map(mid)
        ents = make_entities(mp)
        byidx = {e.idx: e for e in ents}
        sp = [e for e in ents if e.spawned and e.collidable]
        # (a) support graph at spawn
        below = {}
        for s in sp:
            if not s.subject:
                continue
            for o in sp:
                if rests_on(s, o):
                    below[s.idx] = o.idx
                    break    # first candidate wins, as in the binary
        # chains
        depth = {}
        def d(i, seen=()):
            if i in depth:
                return depth[i]
            if i not in below or i in seen:
                depth[i] = 0
                return 0
            depth[i] = 1 + d(below[i], seen + (i,))
            return depth[i]
        for e in sp:
            d(e.idx)
        deep = [i for i, v in depth.items() if v >= 2]
        for i in deep:
            chain = [i]
            while chain[-1] in below:
                chain.append(below[chain[-1]])
            piles.append(dict(map=mid, top=i, chain=chain, names=[byidx[c].name for c in chain],
                              bottom_zmove=(mid, chain[-1]) in zmov))
        for s_idx, o_idx in below.items():
            if (mid, o_idx) in zmov:
                onZ.append(dict(map=mid, passenger=s_idx, pname=byidx[s_idx].name, carrier=o_idx, cname=byidx[o_idx].name))
        # (b) XY movers by programs
        C = mp.C
        movers = set()
        for label, start, lg0, own in roots_of(mp):
            if own is None:
                continue
            seen = census_z.walk(C, start, lg0)
            for pcx, lgs in seen.items():
                op = C[pcx]
                if op in WALK_OPS:
                    for lg in lgs:
                        if lg == ("R", own) or (isinstance(lg, tuple) and lg[0] == "R"):
                            movers.add(lg[1] if isinstance(lg, tuple) else own)
                if op in ANIM_OPS:
                    for lg in lgs:
                        if isinstance(lg, tuple):
                            n = lg[1]
                            e = byidx.get(n)
                            if e is not None and pcx + 1 < len(C):
                                a = C[pcx + 1]
                                if e.speeds.get(a, 0) != 0:
                                    movers.add(n)
        xymov = []
        for n in sorted(movers):
            e = byidx.get(n)
            if e is None:
                continue
            if e.collidable and not (e.flags & 0x100) and e.size[2] > 0 and e.name.startswith(PLATFORM_PREFIX):
                xymov.append(n)
        pas = [e for e in ents if e.spawned and e.subject]
        report[mid] = dict(name=mp.d["name"], zcarriers=sorted(n for (mm, n) in zmov if mm == mid),
                           xycarriers=[(n, byidx[n].name) for n in xymov],
                           passengers_capable=collections.Counter(e.name for e in pas),
                           spawn_edges=len(below), piles=[p for p in piles if p["map"] == mid])
        xy_total += len(xymov)
    json.dump(report, open(os.path.join(HERE, "census2.json"), "w", encoding="utf-8"), indent=1, default=lambda o: dict(o))
    print("maps", len(maps))
    print("spawn-time piles (depth>=2) found:", len(piles))
    for p in piles:
        print("  map %d chain %s names %s bottom-moves-in-Z=%s" % (p["map"], p["chain"], p["names"], p["bottom_zmove"]))
    print("spawn-time passengers resting on a Z-moving carrier:", len(onZ))
    for x in onZ:
        print("  map %d: %s (rec %d) on %s (rec %d)" % (x["map"], x["pname"], x["passenger"], x["cname"], x["carrier"]))
    print("XY-moving platform-class records (heuristic: own programs run a walking opcode or set a moving animation):", xy_total)
    for mid, r in report.items():
        if r["xycarriers"]:
            print("  map %d %s: %s ; spawned passenger-capable on the map: %d" % (mid, r["name"], r["xycarriers"], sum(r["passengers_capable"].values())))
    print("maps with a Z carrier:", sum(1 for r in report.values() if r["zcarriers"]), "records:", len(zmov))


if __name__ == "__main__":
    main()
