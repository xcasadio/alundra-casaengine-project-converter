"""census_g2c2.py - read-only census of the torches (map sprite 137, type 393), the brambles (global sprites 238/239, D34) and the water drops
(map sprite 135, type 391, C71) over data-extracted/data/map_*.json.   Run: python -B -I -X utf8 census_g2c2.py"""
import json, glob, re, collections, os
R = r"D:/development/repo/alundra-casaengine-project-converter"
D = R + "/data-extracted/data"
HERE = os.path.dirname(os.path.abspath(__file__))
out = []
tot = collections.Counter()
per_map = collections.defaultdict(lambda: collections.defaultdict(list))
for f in sorted((p for p in glob.glob(D + '/map_*.json') if re.search(r'map_\d+\.json$', p)), key=lambda p: int(re.search(r'map_(\d+)\.json', p).group(1))):
    n = int(re.search(r'map_(\d+)\.json', f).group(1))
    d = json.load(open(f, encoding='utf-8-sig'))
    si = d['SpriteInfo']
    for k, e in enumerate(si['Entities']['Entities']):
        if e is None:
            continue
        i = e['SpriteTableIndex']
        kind = None
        if i == 137:
            kind = 'torch'
        elif i in (238, 239):
            kind = 'bramble%d' % i
        elif i == 135:
            kind = 'drop'
        if kind is None:
            continue
        # sprite 135/137 are map-page sprites: check the record header to be sure
        if kind in ('torch', 'drop'):
            r = si['SpriteRecords'][i] if i < len(si['SpriteRecords']) else None
            if r is None:
                continue
            h = r['Header']
            if kind == 'torch' and h['ProgramTick'] != 70:
                continue
            if kind == 'drop' and h['ProgramTick'] != 71:
                continue
        script = ''.join(s for s, key in (('A', 'EventCodesA_LoadIndex'), ('B', 'EventCodesB_MapIndex'), ('C', 'EventCodesC_TickIndex'), ('D', 'EventCodesD_TouchIndex'), ('E', 'EventCodesE_DeactivateIndex'), ('F', 'EventCodesF_InteractIndex')) if e[key])
        sc = {key[10]: e[key] for key in ('EventCodesA_LoadIndex', 'EventCodesB_MapIndex', 'EventCodesC_TickIndex', 'EventCodesD_TouchIndex', 'EventCodesE_DeactivateIndex', 'EventCodesF_InteractIndex') if e[key]}
        per_map[n][kind].append((k, e['IsEnabled'], e['XPos'], e['YPos'], e['Height'], e['SpriteDirection'], sc, e['Contents']))
        if e['IsEnabled']:
            tot[kind] += 1
for n in sorted(per_map):
    m = per_map[n]
    out.append('map %d: %s' % (n, ', '.join('%s x%d' % (k, len(v)) for k, v in sorted(m.items()))))
out.append('TOTAL enabled placements: %s' % dict(tot))
out.append('')
out.append('--- torches (map, rec, enabled, x, y, h, dir, scripted programs, contents)')
for n in sorted(per_map):
    for t in per_map[n].get('torch', []):
        out.append('map %3d rec %3d en=%d pos(%3d,%3d,h%2d) dir=%3d scripted=%s contents=%d' % ((n,) + t))
out.append('')
out.append('--- maps that hold at least one torch: brambles / drops')
for n in sorted(per_map):
    m = per_map[n]
    if 'torch' in m:
        out.append('map %3d: torches %d, brambles 238: %d, brambles 239: %d, drops: %d' % (n, len(m['torch']), len(m.get('bramble238', [])), len(m.get('bramble239', [])), len(m.get('drop', []))))
out.append('')
out.append('--- maps with brambles but no torch (map: n238/n239)')
for n in sorted(per_map):
    m = per_map[n]
    if 'torch' not in m and ('bramble238' in m or 'bramble239' in m):
        out.append('map %3d: 238 x%d, 239 x%d' % (n, len(m.get('bramble238', [])), len(m.get('bramble239', []))))
txt = '\n'.join(out)
print(txt)
open(os.path.join(HERE, 'census_g2c2.txt'), 'w', encoding='utf-8').write(txt + '\n')
