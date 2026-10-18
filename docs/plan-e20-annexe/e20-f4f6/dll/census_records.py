"""Census: which map records place a GLOBAL-table sprite (SpriteDirection & 0x80 == 0) whose index is a hero-projectile type
(3..15, 27..29), enabled or not.  Read-only on data-extracted/data.  Output: census_records.txt/json beside this script.
Run: python -B -I census_records.py
"""
import json, os, re, sys, glob

R = 'D:/development/repo/alundra-casaengine-project-converter/'
D = R + 'data-extracted/data/'
OUT = os.path.dirname(os.path.abspath(__file__)) + '/'
WANT = set(range(3, 16)) | {27, 28, 29}
hits = []
nmaps = 0
tot_records = 0
global_records = 0
for p in sorted(glob.glob(D + 'map_*.json'), key=lambda s: int(re.search(r'map_(\d+)\.json$', s).group(1)) if re.search(r'map_(\d+)\.json$', s) else 10**9):
    m = re.search(r'map_(\d+)\.json$', p)
    if not m:
        continue
    idx = int(m.group(1))
    with open(p, encoding='utf-8') as f:
        d = json.load(f)
    nmaps += 1
    ents = d['SpriteInfo']['Entities']['Entities']
    for i, e in enumerate(ents):
        if e is None:
            continue
        tot_records += 1
        sd = e['SpriteDirection']
        if (sd & 0x80) == 0:
            global_records += 1
            if e['SpriteTableIndex'] in WANT:
                hits.append({'map': idx, 'record': i, 'spriteIndex': e['SpriteTableIndex'], 'enabled': e['IsEnabled'],
                             'SpriteDirection': sd, 'pos': [e['XPos'], e['YPos'], e['Height']],
                             'A': e['EventCodesA_LoadIndex'], 'B': e['EventCodesB_MapIndex'], 'C': e['EventCodesC_TickIndex'],
                             'D': e['EventCodesD_TouchIndex'], 'E': e['EventCodesE_DeactivateIndex'], 'F': e['EventCodesF_InteractIndex']})
print('maps', nmaps, 'records', tot_records, 'global-table records', global_records, 'hits', len(hits))
with open(OUT + 'census_records.json', 'w') as f:
    json.dump(hits, f, indent=1)
with open(OUT + 'census_records.txt', 'w') as f:
    f.write(f'maps {nmaps} records {tot_records} global-table records {global_records} hits {len(hits)}\n')
    for h in hits:
        f.write(json.dumps(h) + '\n')
by = {}
for h in hits:
    by.setdefault(h['spriteIndex'], []).append(h['map'])
for k in sorted(by):
    print(k, sorted(set(by[k])), len(by[k]))
