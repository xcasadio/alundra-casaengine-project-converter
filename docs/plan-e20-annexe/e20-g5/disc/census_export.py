"""census_export.py - the same census on the EXPORTED project (alundra-project/Maps/*/*/tilemap/*.tmj, object group 'Entities') and on Data/sprite-records.json; compares with census_g5.json. Read-only."""
import json, glob, os, re, collections
R = 'D:/development/repo/alundra-casaengine-project-converter'
HERE = os.path.dirname(os.path.abspath(__file__))
FAM = {('M', 174): 'PILIER', ('M', 42): 'STATUE', ('G', 229): 'BLOC', ('M', 89): 'POUSSOIR'}
rows = []
n_maps = 0
for f in sorted(glob.glob(R + '/alundra-project/Maps/*/*/tilemap/*.tmj')):
    f = f.replace(chr(92), '/')
    m = re.search(r'-(\d+)\.tmj$', f)
    if not m: continue
    mp = int(m.group(1)); n_maps += 1
    d = json.load(open(f, encoding='utf-8-sig'))
    for l in d['layers']:
        if l['type'] == 'objectgroup' and l['name'] == 'Entities':
            for o in l['objects']:
                p = {q['name']: q['value'] for q in o['properties']}
                page = 'M' if p['SpriteDirection'] & 0x80 else 'G'
                fam = FAM.get((page, p['SpriteTableIndex']))
                if fam:
                    rows.append((fam, mp, p['Index'], p['IsEnabled'], p['SpriteDirection'], p['XPos'], p['YPos'], p['Height'], p['EventCodesA_LoadIndex'], p['EventCodesC_TickIndex'], p['EventCodesD_TouchIndex'], p['EventCodesE_DeactivateIndex'], p['EventCodesF_InteractIndex']))
cen = json.load(open(os.path.join(HERE, 'census_g5.json')))
ref = sorted((r['fam'], r['map'], r['rec'], r['en'], r['dir'], r['x'], r['y'], r['h'], r['A'], r['C'], r['D'], r['E'], r['F']) for r in cen['rows'])
print('exported maps read:', n_maps, '; placements of the 4 families in the export:', len(rows), '; in data-extracted:', len(ref))
print('identical sets:', sorted(rows) == ref)
if sorted(rows) != ref:
    a, b = set(rows), set(ref)
    print('only export:', sorted(a - b)[:5], 'only extracted:', sorted(b - a)[:5])
print(collections.Counter(r[0] for r in rows))
# headers in the exported catalog
sr = json.load(open(R + '/alundra-project/Data/sprite-records.json', encoding='utf-8-sig'))
print('sprite-records.json entries:', len(sr))
k0 = next(iter(sr)); print('entry shape:', json.dumps(sr[k0])[:300])
