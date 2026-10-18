"""Critic census: records that place a GLOBAL-page projectile sprite (3..15, 27..29), and map-local sprites using the E9/C60/E17/C94 codes.
Read-only on data-extracted/data.  python -B -I census_mine.py"""
import json, glob, re
D = 'D:/development/repo/alundra-casaengine-project-converter/data-extracted/data/'
WANT = set(range(1, 16)) | {16, 27, 28, 29}
out = []
nmaps = 0
for p in glob.glob(D + 'map_*.json'):
    m = re.search(r'map_(\d+)\.json$', p)
    if not m:
        continue
    idx = int(m.group(1))
    d = json.load(open(p, encoding='utf-8'))
    nmaps += 1
    ents = d['SpriteInfo']['Entities']['Entities']
    for i, e in enumerate(ents):
        if e is None:
            continue
        sd = e['SpriteDirection']
        if (sd & 0x80) == 0 and e['SpriteTableIndex'] in WANT:
            out.append((idx, i, e['SpriteTableIndex'], e['IsEnabled'], sd, e['XPos'], e['YPos'], e['Height'], e['EventCodesA_LoadIndex'], e['EventCodesB_MapIndex'], e['EventCodesC_TickIndex'], e['EventCodesD_TouchIndex'], e['EventCodesE_DeactivateIndex'], e['EventCodesF_InteractIndex']))
print('maps', nmaps)
for o in sorted(out):
    print(o)
