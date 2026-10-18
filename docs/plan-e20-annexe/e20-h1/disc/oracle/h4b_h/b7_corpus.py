"""b7_corpus.py - which sprite records / placements of the extracted corpus (data-extracted/data/*.json, READ ONLY) carry the native handlers that use each brick.
A record is (page, index): the global page = map_alundra.json (type = index), a map page = map_N.json SpriteRecords[index] (type = 0x100 + index).
A placement = an entity of map_N.json Entities whose SpriteTableIndex has a map-page record in that map (else the global record of that index).
Slots: A = ProgramLoad, C = ProgramTick, D = ProgramTouch, E = ProgramDeactivate, F = ProgramInteract.
usage: python -B b7_corpus.py   -> ../out/corpus_users.json and a printed summary"""
import json, glob, os, collections, sys
DATA = r"D:/development/repo/alundra-casaengine-project-converter/data-extracted/data"
OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', 'out')
SLOT = {'A': 'ProgramLoad', 'C': 'ProgramTick', 'D': 'ProgramTouch', 'E': 'ProgramDeactivate', 'F': 'ProgramInteract'}
INTEREST = {('C', 63), ('C', 65), ('C', 75), ('C', 49), ('C', 51), ('C', 89), ('C', 96), ('C', 39), ('C', 40), ('C', 43), ('C', 50), ('C', 88), ('C', 90),
            ('E', 3), ('E', 13), ('E', 28), ('E', 24), ('D', 43)}
glob_rec = {}
d = json.load(open(os.path.join(DATA, 'map_alundra.json'), encoding='utf-8-sig'))
for r in d['SpriteInfo']['SpriteRecords']:
    if r:
        glob_rec[r['Header']['Sector5Id']] = r['Header']
users = collections.defaultdict(lambda: {'global_records': [], 'map_records': [], 'placements': 0, 'maps_with_placement': set()})
for f in sorted(glob.glob(os.path.join(DATA, 'map_*.json')), key=lambda p: int(os.path.basename(p)[4:-5]) if os.path.basename(p)[4:-5].isdigit() else 0):
    b = os.path.basename(f)
    if not b[4:-5].isdigit():
        continue
    mid = int(b[4:-5])
    m = json.load(open(f, encoding='utf-8-sig'))
    si = m.get('SpriteInfo')
    if not si:
        continue
    recs = si['SpriteRecords']
    for idx, r in enumerate(recs):
        if not r:
            continue
        h = r['Header']
        for s, fld in SLOT.items():
            if (s, h[fld]) in INTEREST:
                users[(s, h[fld])]['map_records'].append((mid, idx))
    ents = (si.get('Entities') or {}).get('Entities') or []
    for e in ents:
        if not e:
            continue
        idx = e['SpriteTableIndex']
        h = recs[idx]['Header'] if idx < len(recs) and recs[idx] else glob_rec.get(idx)
        if not h:
            continue
        for s, fld in SLOT.items():
            if (s, h[fld]) in INTEREST:
                u = users[(s, h[fld])]
                u['placements'] += 1
                u['maps_with_placement'].add(mid)
for idx, h in glob_rec.items():
    for s, fld in SLOT.items():
        if (s, h[fld]) in INTEREST:
            users[(s, h[fld])]['global_records'].append(idx)
res = {}
for k in sorted(users):
    u = users[k]
    res['%s%d' % k] = {'global_records': sorted(u['global_records']), 'map_records': len(u['map_records']), 'distinct_map_record_indices': sorted(set(i for _, i in u['map_records']))[:40],
                       'maps_with_map_record': len(set(mm for mm, _ in u['map_records'])), 'placements': u['placements'], 'maps_with_placement': len(u['maps_with_placement'])}
    print('%s%-3d global records %s | map-page records %d (in %d maps) | placements %d (in %d maps)' % (k[0], k[1], sorted(u['global_records']), len(u['map_records']), len(set(mm for mm, _ in u['map_records'])), u['placements'], len(u['maps_with_placement'])))
json.dump(res, open(os.path.join(OUT, 'corpus_users.json'), 'w'), indent=1)
