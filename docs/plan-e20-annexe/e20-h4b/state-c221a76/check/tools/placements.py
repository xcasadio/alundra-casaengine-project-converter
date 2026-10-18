"""Own counter-check of the 'E3 serves 98 sprites' claim. Read-only on data-extracted/data/map_N.json (483 maps).
Distinguishes (a) couples (map, index) = sprite RECORDS of the map's sprite table (definitions) from (b) static PLACEMENTS = entity records of
SpriteInfo.Entities.Entities, whose page is chosen by SpriteDirection & 0x80 (map page) as Alundra/Entities/Spawning/EntityRecordMapper.cs:236-246.
usage: python -B placements.py"""
import json, glob, os, collections

ROOT = r"D:/development/repo/alundra-casaengine-project-converter/data-extracted/data"
GLOBAL = {3: {16, 23, 150, 151, 152, 153}, 13: {17}, 28: {21}}   # global-page carriers (alundra-project/Data/sprite-records.json, BankKey alundra_N)
couples = collections.defaultdict(list)
place = collections.defaultdict(list)
nmaps = 0
for p in sorted(glob.glob(os.path.join(ROOT, "map_*.json"))):
    name = os.path.basename(p)
    if not name[4:-5].isdigit():
        continue
    d = json.load(open(p, encoding="utf-8"))
    si = d.get("SpriteInfo")
    if not si:
        continue
    nmaps += 1
    recs = si.get("SpriteRecords") or []
    for i, r in enumerate(recs):
        if r and r.get("Header") and r["Header"].get("ProgramDeactivate") in (3, 13, 28):
            couples[r["Header"]["ProgramDeactivate"]].append((name, i))
    for j, e in enumerate(((si.get("Entities") or {}).get("Entities")) or []):
        if not e:
            continue
        idx, sd = e.get("SpriteTableIndex"), e.get("SpriteDirection", 0)
        if idx is None:
            continue
        if sd & 0x80:   # map page
            if idx < len(recs) and recs[idx] and recs[idx].get("Header"):
                code = recs[idx]["Header"].get("ProgramDeactivate")
                if code in (3, 13, 28):
                    place[(code, "map")].append((name, idx, j, e.get("IsEnabled")))
        else:           # global page
            for code, s in GLOBAL.items():
                if idx in s:
                    place[(code, "global")].append((name, idx, j, e.get("IsEnabled")))
print("maps with SpriteInfo:", nmaps)
for c in (3, 13, 28):
    cl = couples[c]
    print("E%d couples (map,index) = sprite records: %d on %d maps, %d distinct indices" % (c, len(cl), len({n for n, _ in cl}), len({i for _, i in cl})))
for k, v in sorted(place.items()):
    per = collections.Counter((x[0], x[1]) for x in v)
    print("E%d placements %s: %d (enabled %d) on maps %s  detail %s" % (k[0], k[1], len(v), sum(1 for x in v if x[3]), sorted({x[0] for x in v}), dict(per)))
