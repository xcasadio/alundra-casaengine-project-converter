"""Critic-own: dump Entities-layer records of a map (read-only). Usage: python -B records.py <mapId> [index ...]"""
import json, os, sys

PROJECT = 'D:/development/repo/alundra-casaengine-project-converter/alundra-project'
idx = json.load(open(PROJECT + '/Maps/world-index.json', encoding='utf-8'))
mid = sys.argv[1]
want = {int(x) for x in sys.argv[2:]}
rel = idx[mid].replace(chr(92), '/')
base = os.path.splitext(os.path.basename(rel))[0]
folder = PROJECT + '/' + os.path.dirname(rel)
d = json.load(open(folder + '/tilemap/' + base + '.tileMap', encoding='utf-8'))
print({k: (v if not isinstance(v, (list, dict)) else '...') for k, v in d.items()})
ents = [o for l in d['object_layers'] if l['name'] == 'Entities' for o in l['objects']]
print('entities', len(ents))
if not want and ents:
    print(json.dumps(ents[0], indent=1)[:1500])
for o in ents:
    cp = o['custom_properties']
    try:
        i = int(cp.get('Index'))
    except Exception:
        i = None
    if i in want:
        print(json.dumps(o)[:1200])
