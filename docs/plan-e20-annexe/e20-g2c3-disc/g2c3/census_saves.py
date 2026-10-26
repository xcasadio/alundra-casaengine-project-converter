"""census_saves.py - read-only: the initialMapId (and camera tile) of every tools/Saves/*.sav, and which of them arrive in a map holding water drops (87, 88, 90, 92, 94, 158) or torches
(86, 87, 88, 89, 90, 92, 93, 94, 441, 442).   Run: python -B -I -X utf8 census_saves.py"""
import json, glob, os
R = r"D:/development/repo/alundra-casaengine-project-converter"
DROP = {87, 88, 90, 92, 94, 158}
TORCH = {86, 87, 88, 89, 90, 92, 93, 94, 441, 442}
rows = []
for f in sorted(glob.glob(R + '/tools/Saves/*.sav')):
    try:
        d = json.load(open(f, encoding='utf-8-sig'))['data']
    except Exception as e:  # noqa
        rows.append((os.path.basename(f), None, None, str(e)))
        continue
    rows.append((os.path.basename(f), d.get('initialMapId'), (d.get('cameraTileX'), d.get('cameraTileY'), d.get('cameraTileZ')), ''))
maps = {}
for n, m, cam, err in rows:
    maps.setdefault(m, []).append(n)
print('saves:', len(rows))
print('--- saves arriving in a map with water drops (%s) or torches (%s)' % (sorted(DROP), sorted(TORCH)))
for n, m, cam, err in rows:
    if m in DROP or m in TORCH:
        print('%-40s map %s camera %s  drops=%s torches=%s' % (n, m, cam, m in DROP, m in TORCH))
print('--- every distinct initialMapId (count)')
print(sorted((k, len(v)) for k, v in maps.items() if k is not None))
