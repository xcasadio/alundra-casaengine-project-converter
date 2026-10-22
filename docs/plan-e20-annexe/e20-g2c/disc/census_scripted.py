"""census_scripted.py - among the placements of the C4 family and of the torches, how many carry a scripted event program (record index != 0 for A/C/D/E/F) that overrides the native header handler.
Run: python -B -I -X utf8 census_scripted.py"""
import json, glob, re, collections, os
R = r"D:/development/repo/alundra-casaengine-project-converter"
D = R + "/data-extracted/data"
tot = collections.Counter(); script = collections.defaultdict(collections.Counter); nat_maps = collections.defaultdict(list)
for f in sorted(p for p in glob.glob(D + '/map_*.json') if re.search(r'map_\d+\.json$', p)):
    n = int(re.search(r'map_(\d+)\.json', f).group(1))
    d = json.load(open(f, encoding='utf-8-sig'))
    si = d['SpriteInfo']; recs = si['SpriteRecords']
    for k, e in enumerate(si['Entities']['Entities']):
        if e is None or not e['IsEnabled']: continue
        i = e['SpriteTableIndex']
        r = recs[i] if i < len(recs) else None
        if r is None: continue
        h = r['Header']
        if h['ProgramTick'] not in (4, 70) or not ((h['CanPickup'] >> 1) & 3): continue
        tot[i] += 1
        flags = tuple(slot for slot, key in (('A', 'EventCodesA_LoadIndex'), ('C', 'EventCodesC_TickIndex'), ('D', 'EventCodesD_TouchIndex'), ('E', 'EventCodesE_DeactivateIndex'), ('F', 'EventCodesF_InteractIndex')) if e[key])
        if flags:
            script[i][''.join(flags)] += 1
        else:
            nat_maps[i].append((n, k))
for i in sorted(tot):
    s = sum(script[i].values())
    print('sprite %3d: %3d enabled placements, %3d with a scripted program (%s), %3d fully native; first native (map, rec): %s' % (i, tot[i], s, dict(script[i]), tot[i] - s, nat_maps[i][:6]))
print('C4 total', sum(v for i, v in tot.items() if i != 137), 'scripted', sum(sum(script[i].values()) for i in tot if i != 137))
