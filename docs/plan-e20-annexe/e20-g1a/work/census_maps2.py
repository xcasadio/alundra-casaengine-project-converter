"""census_maps2.py - liftable placements by family (C,E codes of the header) and kind; chain maps; balance AttackCount of each liftable header. Read-only on the repository."""
import json, glob, re, collections
R = r"D:/development/repo/alundra-casaengine-project-converter"
D = R + "/data-extracted/data"
BAL = json.load(open(R + "/alundra-project/Data/balance.json", encoding='utf-8-sig'))['EntriesBySpriteIndex']
CHAIN = [389, 390, 476, 478, 392, 391, 416, 163, 162, 165, 164, 172, 169, 170, 171, 173, 174, 175, 10, 179, 176, 177, 178, 180, 181, 182, 135, 183, 184, 185]
fam = collections.Counter(); fam_maps = collections.defaultdict(set); bymap = collections.Counter()
sheets = {}
common = {1: ('beans (common 1)', 1, 73, 21), 2: ('bomb (common 2)', 1, 62, 12), 234: ('heavy stone (common 234)', 3, 23, 2)}
for f in sorted(p for p in glob.glob(D + '/map_*.json') if re.search(r'map_\d+\.json$', p)):
    n = int(re.search(r'map_(\d+)\.json', f).group(1))
    d = json.load(open(f, encoding='utf-8-sig'))
    si = d['SpriteInfo']; recs = si['SpriteRecords']
    for e in si['Entities']['Entities']:
        if e is None or not e['IsEnabled']: continue
        i = e['SpriteTableIndex']
        r = recs[i] if i < len(recs) else None
        if r is not None:
            h = r['Header']; kind = (h['CanPickup'] >> 1) & 3
            if kind:
                k = (kind, h['ProgramTick'], h['ProgramDeactivate'], h['MoreFlags'], h['BreakEffect'], h['Contents'], (h['SizeX'], h['SizeY'], h['SizeZ']))
                fam[k] += 1; fam_maps[k].add(n); bymap[n] += 1
                bi = json.load(open(R + "/alundra-project/Data/map-sprite-indices.json", encoding='utf-8-sig')) if not sheets.get('_msi') else sheets['_msi']
                sheets['_msi'] = bi
                idx = bi.get(str(n), [])
                if i < len(idx):
                    b = BAL[idx[i]]
                    sheets[k] = (b['AttackCount'], [(a['AttackType'], a['AttackAttribute'], a['Power']) for a in b['Attacks'][:b['AttackCount']]], b['MaxHp'])
        elif i in common:
            nm, kind, c, e_ = common[i]
            k = (kind, c, e_, nm)
            fam[k] += 1; fam_maps[k].add(n); bymap[n] += 1
tot = sum(fam.values())
print('liftable placements total', tot, 'on', len(bymap), 'maps')
for k, c in sorted(fam.items(), key=lambda kv: -kv[1]):
    print('  %-70s %5d on %3d maps  sheet(AttackCount, attacks, MaxHp)=%s' % (k, c, len(fam_maps[k]), sheets.get(k)))
cm = [m for m in CHAIN if bymap.get(m)]
print('chain maps with liftables: %d of %d, placements %d' % (len(cm), len(CHAIN), sum(bymap[m] for m in cm)))
print('  per chain map:', {m: bymap[m] for m in cm})
print('chain maps without liftable:', [m for m in CHAIN if not bymap.get(m)])
print('maps with >=1 liftable', len(bymap), 'top 12:', bymap.most_common(12))
json.dump({'bymap': bymap, 'fam': {str(k): v for k, v in fam.items()}}, open('lst/census_maps2.json', 'w'))
