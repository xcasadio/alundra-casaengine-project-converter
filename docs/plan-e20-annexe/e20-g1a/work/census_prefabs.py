"""census_prefabs.py - read-only census of the liftable prefabs of alundra-project (Data/sprite-records.json x Entities/*/*.entity)."""
import json, glob, os
R = r"D:/development/repo/alundra-casaengine-project-converter/alundra-project"
rec = json.load(open(R + '/Data/sprite-records.json', encoding='utf-8-sig'))
names = {}
for f in glob.glob(R + '/Entities/*/*.entity'):
    try:
        j = json.load(open(f, encoding='utf-8-sig'))
    except Exception as e:
        continue
    names[j['id']] = j['name']
print('records', len(rec), 'entity files with id', len(names), 'records with a prefab name', sum(1 for k in rec if k in names))
rows = []
for k, v in rec.items():
    cp = v['CanPickup']
    kind = (cp >> 1) & 3
    if kind:
        rows.append((kind, names.get(k, '?'), k, v['MoreFlags'], cp, v['FlagsPortraitShadowType'], v['ProgramTick'], v['ProgramDeactivate'], v['BreakEffect'], v['Contents'], (v['SizeX'], v['SizeY'], v['SizeZ'])))
rows.sort(key=lambda r: (r[0], r[1]))
print('liftable prefabs (kind != 0):', len(rows))
for r in rows:
    print('kind %d  %-48s %s MoreFlags=0x%02X CanPickup=0x%02X C=%s E=%s Break=%s Contents=%s size=%s' % (r[0], r[1], r[2][:8], r[3], r[4], r[6], r[7], r[8], r[9], r[10]))
# all records with kind bits regardless
from collections import Counter
print(Counter(((v['CanPickup'] >> 1) & 3) for v in rec.values()))
