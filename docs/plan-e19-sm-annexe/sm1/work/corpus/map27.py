import json, glob, sys
p = glob.glob('D:/development/repo/alundra-casaengine-project-converter/alundra-project/Maps/Ancient Shrine/Ancient Shrine-27/tilemap/*.tileMap')[0]
d = json.load(open(p, encoding='utf-8'))
for layer in d['object_layers']:
    if layer['name'] != 'Entities':
        continue
    print(sorted(layer['objects'][0]['custom_properties'].keys()))
    for o in layer['objects']:
        cp = o['custom_properties']
        idx = cp.get('Index', None)
        try:
            i = int(idx)
        except Exception:
            continue
        if 1 <= i <= 8:
            print(i, o.get('name'), {k: cp[k] for k in cp if k not in ('Name',)})
