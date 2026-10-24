"""Which sprite pages use C60, C94, C61, E9, E17, E10 (and E1): header codes ProgramTick (C) / ProgramDeactivate (E)."""
import json, os, collections
D = r"D:/development/repo/alundra-casaengine-project-converter/data-extracted/data"
want = {('C', 60), ('C', 94), ('C', 61), ('E', 9), ('E', 17), ('E', 10)}
hits = collections.defaultdict(list)
e1 = collections.defaultdict(set)
def scan(page, n, recs):
    for i, r in enumerate(recs):
        if not r:
            continue
        h = r['Header']
        for key in (('C', h['ProgramTick']), ('E', h['ProgramDeactivate'])):
            if key in want:
                hits[key].append((page, n, i))
        if h['ProgramDeactivate'] == 1:
            e1[page].add((n, i) if page == 'L' else i)
d = json.load(open(D + '/map_alundra.json', encoding='utf-8-sig'))
scan('G', 'alundra', d['SpriteInfo']['SpriteRecords'])
for n in range(0, 483):
    p = os.path.join(D, 'map_%d.json' % n)
    if os.path.exists(p):
        dd = json.load(open(p, encoding='utf-8-sig'))
        scan('L', n, (dd.get('SpriteInfo') or {}).get('SpriteRecords') or [])
for k in sorted(want):
    v = hits[k]
    print(k, 'sprites:', len(v), 'global:', [x[2] for x in v if x[0] == 'G'], 'local:', len([x for x in v if x[0] == 'L']))
print('E1: global indices', sorted(e1['G']), ' local (map,index) count', len(e1['L']))
