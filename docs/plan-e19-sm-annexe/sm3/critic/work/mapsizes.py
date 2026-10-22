"""Critic-own: map_size of every tilemap of the export (read-only). Confirms 'every map is 52 x 60' (R6 / equivalent mutant)."""
import collections
import json
import os

PROJECT = 'D:/development/repo/alundra-casaengine-project-converter/alundra-project'
idx = json.load(open(PROJECT + '/Maps/world-index.json', encoding='utf-8'))
sizes = collections.Counter()
missing = []
for mid, rel in idx.items():
    rel = rel.replace(chr(92), '/')
    base = os.path.splitext(os.path.basename(rel))[0]
    p = PROJECT + '/' + os.path.dirname(rel) + '/tilemap/' + base + '.tileMap'
    try:
        d = json.load(open(p, encoding='utf-8'))
    except Exception as ex:
        missing.append((mid, str(ex)[:60]))
        continue
    ms = d.get('map_size')
    sizes[json.dumps(ms, sort_keys=True)] += 1
print('maps', len(idx), 'sizes', dict(sizes), 'missing', missing[:5], len(missing))
