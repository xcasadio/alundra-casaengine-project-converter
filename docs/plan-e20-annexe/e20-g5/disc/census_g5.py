"""census_g5.py - read-only census of the 4 pushable families over data-extracted/data/map_*.json (483 maps).
Families (type = SpriteTableIndex | 0x100 when SpriteDirection & 0x80, GameEngine.SpawnEntity):
  PILIER   map page 174 -> type 430 (C76 native)
  STATUE   map page  42 -> type 298 (scripted)
  BLOC     global page 229 -> type 229
  POUSSOIR map page  89 -> type 345 (Interrupteur poussoir)
Run: python -B -X utf8 census_g5.py   (writes census_g5.txt and census_g5.json next to itself)"""
import json, glob, re, os, collections, sys
R = r"D:/development/repo/alundra-casaengine-project-converter"
D = R + "/data-extracted/data"
HERE = os.path.dirname(os.path.abspath(__file__))
FAM = {('M', 174): 'PILIER', ('M', 42): 'STATUE', ('G', 229): 'BLOC', ('M', 89): 'POUSSOIR'}
glob_doc = json.load(open(D + '/map_alundra.json', encoding='utf-8-sig'))
GREC = glob_doc['SpriteInfo']['SpriteRecords']
out = []; rows = []
hdr = {}
for f in sorted(p for p in glob.glob(D + '/map_*.json') if re.search(r'map_\d+\.json$', p)):
    n = int(re.search(r'map_(\d+)\.json', f).group(1))
    d = json.load(open(f, encoding='utf-8-sig'))
    si = d['SpriteInfo']; recs = si['SpriteRecords']
    ents = si['Entities']['Entities']
    for k, e in enumerate(ents):
        if e is None: continue
        page = 'M' if (e['SpriteDirection'] & 0x80) else 'G'
        i = e['SpriteTableIndex']
        fam = FAM.get((page, i))
        if not fam: continue
        r = (recs if page == 'M' else GREC)[i]
        h = r['Header']
        hdr[fam] = h
        rows.append(dict(fam=fam, map=n, rec=k, en=e['IsEnabled'], dir=e['SpriteDirection'], x=e['XPos'], y=e['YPos'], h=e['Height'],
                         zone=(e['XMin'], e['XMax'], e['YMin'], e['YMax']), A=e['EventCodesA_LoadIndex'], B=e['EventCodesB_MapIndex'], C=e['EventCodesC_TickIndex'],
                         D=e['EventCodesD_TouchIndex'], E=e['EventCodesE_DeactivateIndex'], F=e['EventCodesF_InteractIndex'], u10=e['_10'], contents=e['Contents'],
                         mapname=d['Info'].get('MapId')))
json.dump(dict(rows=rows, headers=hdr), open(os.path.join(HERE, 'census_g5.json'), 'w'), indent=1)
by = collections.defaultdict(list)
for r in rows: by[r['fam']].append(r)
for fam, hh in hdr.items():
    out.append('%s header: %s' % (fam, {k: v for k, v in hh.items() if k != 'Ubuff'}))
for fam, rs in by.items():
    en = [r for r in rs if r['en']]
    maps = sorted({r['map'] for r in en})
    out.append('')
    out.append('== %s: %d records (%d enabled) on %d maps (enabled): %s' % (fam, len(rs), len(en), len(maps), maps))
    for r in rs:
        scr = ''.join(s for s in 'ABCDEF' if r[s])
        out.append('  map %3d rec %3d en=%d dir=0x%02X (spawn@load=%d dirbits=%d) pos(%3d,%3d,h%2d) zone[%d..%d]x[%d..%d] idx A%d B%d C%d D%d E%d F%d u10=%d contents=%d scripted=%s' % (
            r['map'], r['rec'], r['en'], r['dir'], 1 if r['dir'] & 0x40 else 0, r['dir'] & 3, r['x'], r['y'], r['h'], *r['zone'], r['A'], r['B'], r['C'], r['D'], r['E'], r['F'], r['u10'], r['contents'], scr or '-'))
txt = '\n'.join(out)
open(os.path.join(HERE, 'census_g5.txt'), 'w', encoding='utf-8').write(txt + '\n')
print(txt)
