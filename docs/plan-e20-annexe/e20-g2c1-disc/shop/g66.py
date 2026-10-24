"""g66.py : header and attack data of the global shop sprites in data-extracted/data/map_alundra.json. READ-ONLY."""
import json, sys
d = json.load(open('D:/development/repo/alundra-casaengine-project-converter/data-extracted/data/map_alundra.json', encoding='utf-8-sig'))
recs = d['SpriteInfo']['SpriteRecords']
print(len(recs))
for i in (48, 50, 66, 67, 68, 69, 71, 81, 83, 85, 113):
    r = recs[i]
    h = r['Header']
    print(i, {k: v for k, v in h.items()})
    print('   other keys:', {k: (len(v) if isinstance(v, list) else v) for k, v in r.items() if k != 'Header'})
    break
