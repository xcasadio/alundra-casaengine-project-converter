"""census_c.py - E20.g2b2 T0 (c): the sprite types of the export that have the gravity (Flags & 0x100) and DestroyOnVramFlags (Flags & 0x200000).
Flags = MoreFlags | CanPickup << 8 | FlagsPortraitShadowType << 16 (EntitySpawnFactory.cs: ApplyHeaderInitialization); the type is index | 0x100 on the map page (BankKey "N"), index on the global page (BankKey "alundra_N")
(WorldProxy.SpawnEntityBySpriteIndex / EntitySpawnFactory.CreateEntityFromSpriteIndex). Source: alundra-project/Data/sprite-records.json (the export) ; the placements by the maps come from data-extracted/data/map_*.json (the SpriteTableIndex of the enabled placements).
Run: python -B -X utf8 docs/plan-e20-annexe/e20-g2b2/census_c.py   (from the repository root)"""
import json, glob, re, collections
R = '.'
recs = json.load(open(R + '/alundra-project/Data/sprite-records.json', encoding='utf-8-sig')).values()
mapidx = json.load(open(R + '/alundra-project/Data/map-sprite-indices.json', encoding='utf-8-sig'))
placed = collections.defaultdict(lambda: collections.Counter())
for f in sorted(glob.glob(R + '/data-extracted/data/map_*.json')):
    if not re.search(r'map_\d+\.json$', f):
        continue
    n = int(re.search(r'map_(\d+)\.json', f).group(1))
    si = json.load(open(f, encoding='utf-8-sig')).get('SpriteInfo')
    if not si:
        continue
    for e in si['Entities']['Entities']:
        if e is not None and e['IsEnabled']:
            placed[e['SpriteTableIndex']][n] += 1
usable = collections.defaultdict(list)   # map-page index -> maps whose table holds it
for m, l in mapidx.items():
    for i in l:
        usable[i].append(int(m))
rows = []
for r in recs:
    bk = r.get('BankKey') or ''
    glob_page = bk.startswith('alundra_')
    try:
        idx = int(bk.replace('alundra_', ''))
    except ValueError:
        continue
    fl = (r['MoreFlags'] | (r['CanPickup'] << 8) | (r['FlagsPortraitShadowType'] << 16)) & 0xFFFFFFFF
    if fl & 0x100 and fl & 0x200000:
        typ = idx if glob_page else 0x100 | idx
        rows.append(dict(page='global' if glob_page else 'map', idx=idx, type=typ, flags=fl, genre=(r['CanPickup'] >> 1) & 3, C=r['ProgramTick'], E=r['ProgramDeactivate'], touch=r['ProgramTouch'], brk=r['BreakEffect'],
                         placements=0 if glob_page else sum(placed[idx].values()), maps=0 if glob_page else len(placed[idx]), usable=0 if glob_page else len(usable[idx])))
rows.sort(key=lambda d: (d['page'], d['idx']))
print('gravity and 0x200000: %d types (%d map page, %d global page)' % (len(rows), sum(r['page'] == 'map' for r in rows), sum(r['page'] == 'global' for r in rows)))
print('page   idx  type  flags      genre   C    E  touch brk  placements(maps)  maps whose table holds the index')
for d in rows:
    print('%-6s %3d  %4d  0x%06X   %d    %3d  %3d  %3d  %3d   %5d (%3d)   %3d' % (d['page'], d['idx'], d['type'], d['flags'], d['genre'], d['C'], d['E'], d['touch'], d['brk'], d['placements'], d['maps'], d['usable']))
