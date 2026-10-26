"""scan_c62_placements.py - E20.h3b T0 (e): which maps PLACE an entity whose sprite header has ProgramTick = 62 (C = 62; the bomb, global sprite type 2), by page, and for each placement whether the
record carries a map program of its own in the slot C (EventCodesC_TickIndex != 0: the registry of the native handlers is then never consulted, EventProgramRunner.RunSpriteEvent tests
`ProgramIndexes[slot] & 0x7f == 0`), and whether the map is on the 30 maps of the story chain.  Same method as docs/plan-e20-annexe/e20-h3a/scan_e19_placements.py.
usage: python -I -B scan_c62_placements.py"""
import json, glob, re, sys, collections
sys.stdout.reconfigure(encoding='utf-8')
ROOT = "D:/development/repo/alundra-casaengine-project-converter/data-extracted/data"
CHAIN = [389, 390, 476, 478, 392, 391, 416, 163, 162, 165, 164, 172, 169, 170, 171, 173, 174, 175, 10, 179, 176, 177, 178, 180, 181, 182, 135, 183, 184, 185]
g = json.load(open(ROOT + "/map_alundra.json", encoding='utf-8-sig'))
gh = {x['Header']['Sector5Id']: x['Header'] for x in g['SpriteInfo']['SpriteRecords'] if x}
print('global sprite types with C = 62:', sorted(k for k, h in gh.items() if h['ProgramTick'] == 62))
res = collections.defaultdict(list)
n_maps = 0
for f in sorted(glob.glob(ROOT + "/map_*.json")):
    m = re.search(r'map_(\d+)\.json$', f.replace(chr(92), '/'))
    if not m:
        continue
    mid = int(m.group(1))
    d = json.load(open(f, encoding='utf-8-sig'))
    n_maps += 1
    maph = {x['Header']['Sector5Id']: x['Header'] for x in d['SpriteInfo']['SpriteRecords'] if x}
    table = d['SpriteInfo']['SpriteTable']
    for i, en in enumerate(d['SpriteInfo']['Entities']['Entities']):
        if not en:
            continue
        t = en['SpriteTableIndex']
        mappage = 0 <= t < len(table) and table[t] != -1
        h = maph.get(t) if mappage else gh.get(t)
        if h is not None and h['ProgramTick'] == 62:
            res[mid].append((i, (t | 0x100) if mappage else t, 'map-page' if mappage else 'global', en['EventCodesC_TickIndex']))
print('maps scanned:', n_maps)
print('maps with a placement of a sprite whose header has C = 62:', len(res))
for mid in sorted(res):
    print('  map', mid, 'ON THE CHAIN' if mid in CHAIN else '', res[mid])
print('placed types:', sorted({t for v in res.values() for _, t, _, _ in v}))
print('placements by page:', dict(collections.Counter(pg for v in res.values() for _, _, pg, _ in v)))
print('placements:', sum(len(v) for v in res.values()), '; with a map program of their own in the slot C (EventCodesC_TickIndex != 0, native C62 never reached):', sum(1 for v in res.values() for *_, c in v if c),
      '; without (native C62 runs from the load):', sum(1 for v in res.values() for *_, c in v if not c))
print('on the 30 chain maps:', {m: res[m] for m in CHAIN if m in res})
