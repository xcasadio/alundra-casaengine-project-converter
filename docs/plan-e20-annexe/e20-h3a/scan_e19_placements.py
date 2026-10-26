"""scan_e19_placements.py - E20.h3a T0 (b): which maps PLACE an entity whose sprite header has ProgramDeactivate = 19 (E = 19), by page (global page 241-243, or the map page with the
type index | 0x100 that SpawnWarpEntity builds), and whether one of those maps is on the 30 maps of the story chain.  usage: python -I -B scan_e19_placements.py"""
import json
import glob
import re
import sys
import collections
sys.stdout.reconfigure(encoding='utf-8')
ROOT = "D:/development/repo/alundra-casaengine-project-converter/data-extracted/data"
CHAIN = [389, 390, 476, 478, 392, 391, 416, 163, 162, 165, 164, 172, 169, 170, 171, 173, 174, 175, 10, 179, 176, 177, 178, 180, 181, 182, 135, 183, 184, 185]
g = json.load(open(ROOT + "/map_alundra.json", encoding='utf-8-sig'))
gh = {x['Header']['Sector5Id']: x['Header'] for x in g['SpriteInfo']['SpriteRecords'] if x}
print('global sprite types with E = 19:', sorted(k for k, h in gh.items() if h['ProgramDeactivate'] == 19))
rows = collections.defaultdict(list)
for f in sorted(glob.glob(ROOT + "/map_*.json")):
    m = re.search(r'map_(\d+)\.json$', f.replace(chr(92), '/'))
    if not m:
        continue
    mid = int(m.group(1))
    d = json.load(open(f, encoding='utf-8-sig'))
    maph = {x['Header']['Sector5Id']: x['Header'] for x in d['SpriteInfo']['SpriteRecords'] if x}
    ents = d['SpriteInfo']['Entities']['Entities']
    for i, en in enumerate(ents):
        if not en:
            continue
        keys = list(en.keys())
        rows[mid].append((i, en))
sample = next(iter(rows.values()))[0][1]
print('entity record keys:', list(sample.keys()))

# placements: a placement names an index of the map's SpriteTable; an entry other than -1 means the sprite is read in the page of the map (type = index | 0x100), -1 the global page
res = collections.defaultdict(list)
for f in sorted(glob.glob(ROOT + "/map_*.json")):
    m = re.search(r'map_(\d+)\.json$', f.replace(chr(92), '/'))
    if not m:
        continue
    mid = int(m.group(1))
    d = json.load(open(f, encoding='utf-8-sig'))
    maph = {x['Header']['Sector5Id']: x['Header'] for x in d['SpriteInfo']['SpriteRecords'] if x}
    table = d['SpriteInfo']['SpriteTable']
    for i, en in rows[mid]:
        t = en['SpriteTableIndex']
        mappage = 0 <= t < len(table) and table[t] != -1
        h = maph.get(t) if mappage else gh.get(t)
        if h is not None and h['ProgramDeactivate'] == 19:
            res[mid].append((i, (t | 0x100) if mappage else t, 'map-page' if mappage else 'global', en['EventCodesE_DeactivateIndex']))
print('maps with a placement of a sprite whose header has E = 19:', len(res))
for mid in sorted(res):
    print('  map', mid, 'ON THE CHAIN' if mid in CHAIN else '', res[mid])
print('placed types (all maps):', sorted({t for v in res.values() for _, t, _, _ in v}))
print('placements by page:', collections.Counter(pg for v in res.values() for _, _, pg, _ in v))
print('placements with an own map program in the slot E (EventCodesE_DeactivateIndex != 0):', sum(1 for v in res.values() for *_, e in v if e))
print('on the 30 chain maps:', {m: res[m] for m in CHAIN if m in res})
