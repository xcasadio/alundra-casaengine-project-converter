"""census_maps.py - read-only census of the liftable placements in data-extracted/data/map_N.json (SpriteInfo.SpriteRecords headers x SpriteInfo.Entities.Entities)."""
import json, glob, os, re, collections
D = r"D:/development/repo/alundra-casaengine-project-converter/data-extracted/data"
SPR = json.load(open(r"D:/development/repo/alundra-casaengine-project-converter/alundra-project/Data/sprite-records.json", encoding='utf-8-sig'))
NAMES = {}
for f in glob.glob(r"D:/development/repo/alundra-casaengine-project-converter/alundra-project/Entities/*/*.entity"):
    j = json.load(open(f, encoding='utf-8-sig')); NAMES[j['id']] = j['name']
SIG = {}
for k, v in SPR.items():
    if (v['CanPickup'] >> 1) & 3:
        SIG[(v['MoreFlags'], v['CanPickup'], v['FlagsPortraitShadowType'], v['ProgramTick'], v['ProgramDeactivate'], v['BreakEffect'], v['Contents'], v['SizeX'], v['SizeY'], v['SizeZ'])] = NAMES[k]
BAL = json.load(open(r"D:/development/repo/alundra-casaengine-project-converter/alundra-project/Data/balance.json", encoding='utf-8-sig'))['EntriesBySpriteIndex']
MSI = json.load(open(r"D:/development/repo/alundra-casaengine-project-converter/alundra-project/Data/map-sprite-indices.json", encoding='utf-8-sig'))
per_name = collections.Counter(); maps_name = collections.defaultdict(set); per_map = collections.Counter()
hdr_seen = {}
unmatched = collections.Counter()
for f in sorted([p for p in glob.glob(D + '/map_*.json') if re.search(r'map_\d+\.json$', p)], key=lambda p: int(re.search(r'map_(\d+)\.json', p).group(1))):
    n = int(re.search(r'map_(\d+)\.json', f).group(1))
    d = json.load(open(f, encoding='utf-8-sig'))
    si = d['SpriteInfo']
    recs = si['SpriteRecords']
    lifts = {}
    for i, r in enumerate(recs):
        if r is None:
            continue
        h = r['Header']
        kind = (h['CanPickup'] >> 1) & 3
        if kind:
            sig = (h['MoreFlags'], h['CanPickup'], h['FlagsPortraitShadowType'], h['ProgramTick'], h['ProgramDeactivate'], h['BreakEffect'], h['Contents'], h['SizeX'], h['SizeY'], h['SizeZ'])
            nm = SIG.get(sig)
            if nm is None:
                unmatched[sig] += 1
                nm = '?%s' % (sig,)
            lifts[i] = (kind, nm, h)
    if not lifts:
        continue
    for e in si['Entities']['Entities']:
        if e is None:
            continue
        i = e['SpriteTableIndex']
        if i in lifts and e.get('IsEnabled', 1):
            kind, nm, h = lifts[i]
            per_name[nm] += 1; maps_name[nm].add(n); per_map[n] += 1
            bi = MSI.get(str(n), [])
            if i < len(bi):
                b = BAL[bi[i]]
                hdr_seen[nm] = (kind, bi[i], b['AttackCount'], [a['Power'] for a in b['Attacks'][:max(1, b['AttackCount'])]], b['MaxHp'])
print('placements per prefab (enabled records):')
tot = 0
for nm, c in per_name.most_common():
    tot += c
    print('  %-48s %5d placements on %3d maps   balance(kind, idx, AttackCount, powers, MaxHp)=%s' % (nm, c, len(maps_name[nm]), hdr_seen.get(nm)))
print('total placements', tot, 'on', len(per_map), 'maps')
print('unmatched signatures', dict(unmatched))
json.dump({'per_map': per_map, 'per_name': per_name, 'maps_name': {k: sorted(v) for k, v in maps_name.items()}}, open('lst/census_maps.json', 'w'))
