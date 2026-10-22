"""The portals of the export that lead INTO the given maps: (source map, portal index, DestTileX, DestTileY, ZLevel, Flags). A valid hero arrival tile for an arc.
Usage: python -B arrivals.py MAP [MAP ...]"""
import json, glob, sys, os
ROOT = 'D:/development/repo/alundra-casaengine-project-converter/alundra-project'
want = set(int(x) for x in sys.argv[1:])
for f in sorted(glob.glob(ROOT + '/Maps/*/*/tilemap/*.tileMap')):
    try:
        d = json.load(open(f, encoding='utf-8'))
    except Exception:
        continue
    for layer in d.get('object_layers', []):
        if layer['name'] != 'Portals':
            continue
        for o in layer['objects']:
            cp = o['custom_properties']
            try:
                dest = int(cp.get('DestMapId'))
            except Exception:
                continue
            if dest in want:
                print('into', dest, 'from', os.path.basename(os.path.dirname(os.path.dirname(f))), 'portal', cp.get('Index'), 'tile', (cp.get('DestTileX'), cp.get('DestTileY')), 'z', cp.get('ZLevel'), 'flags', cp.get('Flags'))
