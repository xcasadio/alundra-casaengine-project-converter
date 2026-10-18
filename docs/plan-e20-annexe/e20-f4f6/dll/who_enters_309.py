"""Which maps have a portal whose destination is map 309, and which map events/scripts name it is out of scope.  python -B who_enters_309.py   (read-only)"""
import glob, json, os, re
R = 'D:/development/repo/alundra-casaengine-project-converter/alundra-project/Maps/'
hits = []
for f in sorted(glob.glob(R + '*/*/tilemap/*.tileMap')):
    base = os.path.basename(os.path.dirname(os.path.dirname(f)))
    mm = re.search(r'-(\d+)$', base)
    if not mm:
        continue
    mid = int(mm.group(1))
    d = json.load(open(f, encoding='utf-8'))
    for layer in d.get('object_layers', []):
        if layer.get('name') != 'Portals':
            continue
        for o in layer.get('objects', []):
            cp = o.get('custom_properties', {})
            if cp.get('DestMapId') == '309':
                hits.append((mid, cp))
print('portals to 309:', len(hits))
for mid, cp in hits:
    print(' from map', mid, cp)
