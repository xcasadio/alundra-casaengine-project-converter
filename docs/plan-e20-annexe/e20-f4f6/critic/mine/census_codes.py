"""Critic census: which sprites (global page and map-local pages of the 483 maps) carry the codes C60 C61 C94 E9 E10 E17 and E1 (and which use E1 with BreakEffect).
python -B -I census_codes.py"""
import json, glob, re
D = 'D:/development/repo/alundra-casaengine-project-converter/data-extracted/data/'
WANT = {('C', 60), ('C', 61), ('C', 94), ('E', 9), ('E', 10), ('E', 17), ('E', 1), ('E', 0)}
glob_hits = {}
local_hits = {}
nmaps = 0
for p in glob.glob(D + 'map_*.json'):
    m = re.search(r'map_(\d+)\.json$', p)
    if not m:
        continue
    idx = int(m.group(1))
    d = json.load(open(p, encoding='utf-8'))
    nmaps += 1
    recs = d['SpriteInfo']['SpriteRecords']
    for t, r in enumerate(recs):
        if r is None:
            continue
        h = r['Header']
        pairs = [('C', h['ProgramTick']), ('E', h['ProgramDeactivate'])]
        for k in pairs:
            if k in WANT:
                if idx == 14 or True:
                    pass
                key = (t, k)
                if p.endswith('map_alundra.json'):
                    glob_hits.setdefault(key, set()).add('alundra')
                else:
                    local_hits.setdefault(key, set()).add(idx)
print('maps', nmaps)
print('--- map_alundra.json (global page)')
for k in sorted(glob_hits):
    print(k)
print('--- local pages (per map file); the map_alundra file is also in the glob so skip)')
by = {}
for (t, k), maps in local_hits.items():
    by.setdefault(k, []).append((t, len(maps)))
for k in sorted(by):
    print(k, 'sprites', len(by[k]), 'total (sprite,map) pairs', sum(n for _, n in by[k]), 'first few', sorted(by[k])[:5])
