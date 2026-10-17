#!/usr/bin/env python3
"""critic-complet c7: per map, liftable records (kind = (CanPickup >> 1) & 3 != 0 of the header SpriteRecords[SpriteTableIndex], map 'alundra'
common bank for indices the map does not hold is NOT resolved here) in data-extracted, versus the export census (../dll/all_records.json).
Prints the maps where the per-map counts differ, with the records. Read-only, stdout utf-8."""
import glob, json, os, re, sys
from collections import Counter, defaultdict
sys.stdout.reconfigure(encoding='utf-8')
HERE = os.path.dirname(os.path.abspath(__file__))
DATA = 'D:/development/repo/alundra-casaengine-project-converter/data-extracted/data'
recs = json.load(open(os.path.join(HERE, '..', 'dll', 'all_records.json'), encoding='utf-8'))
exp = defaultdict(set)
for r in recs:
    if r['kind']:
        exp[r['map']].add(r['idx'])
tot = 0
unresolved = Counter()
for f in sorted(glob.glob(DATA + '/map_*.json')):
    m = re.search(r'map_(\d+)\.json$', f)
    if not m:
        continue
    mp = int(m.group(1))
    d = json.load(open(f, encoding='utf-8-sig'))
    si = d.get('SpriteInfo') or {}
    ents = [e for e in ((si.get('Entities') or {}).get('Entities') or []) if e is not None]
    sr = si.get('SpriteRecords') or []
    mine = set()
    for i, e in enumerate(ents):
        k = e['SpriteTableIndex']
        h = sr[k] if 0 <= k < len(sr) else None
        if h is None:
            unresolved[mp] += 1
            continue
        if (h['Header']['CanPickup'] >> 1) & 3:
            mine.add(i)
    tot += len(mine)
    if mine != exp.get(mp, set()):
        print('map %d: data-extracted %s ; export %s' % (mp, sorted(mine ^ exp.get(mp, set())), ''))
print('total data-extracted (map-local headers only): %d ; export census: %d ; records with a non-local header: %d' % (
    tot, sum(len(v) for v in exp.values()), sum(unresolved.values())))
