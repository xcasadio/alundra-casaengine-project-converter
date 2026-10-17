"""census_codes.py - which headers use C23, C4, C70, C73, C62, E2, E12, E20, E21 (any kind) and which of them are liftable; page indices per family. Read-only on the repository."""
import json, glob, re, collections
R = r"D:/development/repo/alundra-casaengine-project-converter"
D = R + "/data-extracted/data"
C_CODES = (23, 4, 70, 73, 62); E_CODES = (2, 12, 20, 21)
idx_by = collections.defaultdict(lambda: collections.defaultdict(set))   # (slot,code) -> kind -> set(idx)
nonlift = collections.defaultdict(list)
for f in sorted(p for p in glob.glob(D + '/map_*.json') if re.search(r'map_\d+\.json$', p)):
    n = int(re.search(r'map_(\d+)\.json', f).group(1))
    d = json.load(open(f, encoding='utf-8-sig'))
    si = d['SpriteInfo']; recs = si['SpriteRecords']
    used = collections.Counter(e['SpriteTableIndex'] for e in si['Entities']['Entities'] if e and e['IsEnabled'])
    for i, r in enumerate(recs):
        if r is None: continue
        h = r['Header']; kind = (h['CanPickup'] >> 1) & 3
        for slot, code in (('C', h['ProgramTick']), ('E', h['ProgramDeactivate'])):
            if (slot == 'C' and code in C_CODES) or (slot == 'E' and code in E_CODES):
                idx_by[(slot, code)][kind].add(i)
                if kind == 0:
                    nonlift[(slot, code)].append((n, i, used.get(i, 0)))
for k in sorted(idx_by):
    print(k, {kind: sorted(v) for kind, v in idx_by[k].items()})
    if nonlift[k]:
        print('   NON-liftable records with that code:', nonlift[k][:12], '... total', len(nonlift[k]))
