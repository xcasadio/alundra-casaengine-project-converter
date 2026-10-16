"""records.py - the enabled records of the five types over all maps (data-extracted/data/map_N.json, read only)."""
import json, glob, os, re, csv
from collections import defaultdict
ROOT = r"D:/development/repo/alundra-casaengine-project-converter/"
TYPES = {136: 392, 199: 455, 85: 341, 82: 338, 88: 344}
NAMES = {392: 'Poisson', 455: 'Guepe', 341: 'Orc masse', 338: 'Orc hache', 344: 'Orc armure de fer'}
# map names
names = {}
for r in csv.DictReader(open(ROOT + 'docs/plan-e14-annexe/e14-roster/roster.tsv', encoding='utf-8'), delimiter='\t'):
    pass
rows = []
invalid = []
for f in [g for g in glob.glob(ROOT + 'data-extracted/data/map_*.json') if re.search(r'map_[0-9]+[.]json$', g)]:
    mid = int(re.search(r'map_([0-9]+)[.]json', f).group(1))
    d = json.load(open(f, encoding='utf-8-sig'))
    recs = d['SpriteInfo']['Entities']['Entities']
    for i, r in enumerate(recs):
        if not r or not r['IsEnabled']:
            continue
        t = TYPES.get(r['SpriteTableIndex'])
        if t is None:
            continue
        if d['SpriteInfo']['SpriteTable'][r['SpriteTableIndex']] == -1 or not d['SpriteInfo']['SpriteRecords'][r['SpriteTableIndex']]:
            invalid.append((mid, i, t)); continue
        rows.append((mid, i, t, r['XPos'], r['YPos'], r['Height'], r['SpriteDirection'], r['EventCodesA_LoadIndex'], r['EventCodesC_TickIndex'], r['EventCodesD_TouchIndex'], r['EventCodesE_DeactivateIndex'], r['EventCodesF_InteractIndex'], r['Contents']))
rows.sort()
bytype = defaultdict(list)
for r in rows: bytype[r[2]].append(r)
out = []
for t in (392, 455, 341, 338, 344):
    rs = bytype[t]
    maps = sorted(set(r[0] for r in rs))
    native = sum(1 for r in rs if (r[8] & 0x7F) == 0 and (r[9] & 0x7F) == 0)
    cscr = sum(1 for r in rs if (r[8] & 0x7F) != 0)
    dscr = sum(1 for r in rs if (r[9] & 0x7F) != 0)
    auto = sum(1 for r in rs if r[6] & 0x40)
    out.append('type %d %s: %d enabled records on %d maps; pure native C and D: %d; scripted C: %d; scripted D: %d; auto-spawn (SpriteDirection&0x40): %d' % (t, NAMES[t], len(rs), len(maps), native, cscr, dscr, auto))
    out.append('   maps: ' + ', '.join('%d(x%d)' % (m, sum(1 for r in rs if r[0] == m)) for m in maps))
    for r in rs:
        if (r[8] & 0x7F) != 0 or (r[9] & 0x7F) != 0 or (r[7] & 0x7F) != 0 or (r[10] & 0x7F) != 0:
            out.append('   scripted codes: map %d record %d: A%d C%d D%d E%d F%d dir=0x%02X' % (r[0], r[1], r[7], r[8], r[9], r[10], r[11], r[6]))
out.append('invalid records (SpriteTable entry -1 on that map, never spawned): %s' % invalid)
open('records_b1.txt', 'w', encoding='utf-8').write('\n'.join(out) + '\n')
print('\n'.join(out))
print(sum(len(v) for v in bytype.values()), 'records in total')
