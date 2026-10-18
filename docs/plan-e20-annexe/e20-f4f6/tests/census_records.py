# Census of map records that place a common-bank (global page) sprite of the hero projectile set.
# Read-only: reads data-extracted/data/map_N.json. Output: summary on stdout.
import json, glob, os, re, sys
ROOT = 'D:/development/repo/alundra-casaengine-project-converter/data-extracted/data'
SET = {3,4,5,6,7,8,9,10,11,12,13,14,15,27,28,29}
hits = {}
total = 0
for p in sorted(glob.glob(ROOT + '/map_*.json')):
    m = re.match(r'map_(\d+)\.json$', os.path.basename(p))
    if not m: continue
    mid = int(m.group(1))
    try:
        d = json.load(open(p, encoding='utf-8'))
    except Exception as e:
        continue
    total += 1
    ents = (d.get('SpriteInfo') or {}).get('Entities') or {}
    ents = ents.get('Entities') if isinstance(ents, dict) else ents
    for i, e in enumerate(ents or []):
        if e is None: continue
        if (e['SpriteDirection'] & 0x80) == 0 and e['SpriteTableIndex'] in SET:
            hits.setdefault(mid, []).append((i, e['SpriteTableIndex'], e['XPos'], e['YPos'], e['Height'], e['IsEnabled']))
print('maps read', total)
for mid, lst in sorted(hits.items()):
    print(mid, lst)
