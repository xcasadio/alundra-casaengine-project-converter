"""Print the object layers other than Entities of one map: the map events (X1..Y2, program B) and the portals.
Usage: python -B mapevents.py MAP"""
import json, glob, sys, os
REPO = "D:/development/repo/alundra-casaengine-project-converter"
BS = chr(92)


def find_tilemap(mapno):
    idx = json.load(open(REPO + '/alundra-project/Maps/world-index.json', encoding='utf-8'))
    rel = idx[str(mapno)].replace(BS, '/')
    folder = os.path.dirname(REPO + '/alundra-project/' + rel)
    return folder, os.path.splitext(os.path.basename(rel))[0]


if __name__ == '__main__':
    mapno = int(sys.argv[1])
    folder, name = find_tilemap(mapno)
    p = glob.glob(folder + '/tilemap/*.tileMap')
    d = json.load(open(p[0], encoding='utf-8'))
    print('map', mapno, name, 'layers', [l['name'] for l in d['object_layers']], 'size', d.get('width'), d.get('height'))
    for layer in d['object_layers']:
        if layer['name'] == 'Entities':
            continue
        print('--', layer['name'], len(layer['objects']))
        for o in layer['objects']:
            cp = o['custom_properties']
            print('  ', {k: cp[k] for k in cp})
