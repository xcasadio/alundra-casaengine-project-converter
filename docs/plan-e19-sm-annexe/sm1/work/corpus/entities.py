"""Print the Entities layer of one map. Usage: python -B entities.py MAP [index ...]"""
import json, glob, sys, re, os
REPO = "D:/development/repo/alundra-casaengine-project-converter"
BS = chr(92)


def find_tilemap(mapno):
    idx = json.load(open(REPO + '/alundra-project/Maps/world-index.json', encoding='utf-8'))
    rel = idx[str(mapno)].replace(BS, '/')
    folder = os.path.dirname(REPO + '/alundra-project/' + rel)
    return folder, os.path.splitext(os.path.basename(rel))[0]


if __name__ == '__main__':
    mapno = int(sys.argv[1])
    only = set(int(x) for x in sys.argv[2:])
    folder, name = find_tilemap(mapno)
    print('folder', folder, 'name', name)
    p = glob.glob(folder + '/tilemap/*.tileMap')
    d = json.load(open(p[0], encoding='utf-8'))
    for layer in d['object_layers']:
        if layer['name'] != 'Entities':
            continue
        for o in layer['objects']:
            cp = o['custom_properties']
            try:
                i = int(cp.get('Index'))
            except Exception:
                continue
            if only and i not in only:
                continue
            progs = ''.join('%s%s ' % (k[11], cp[k]) for k in ('EventCodesA_LoadIndex', 'EventCodesB_MapIndex', 'EventCodesC_TickIndex', 'EventCodesD_TouchIndex', 'EventCodesE_DeactivateIndex', 'EventCodesF_InteractIndex'))
            print(i, 'spr', cp.get('SpriteTableIndex'), 'en', cp.get('IsEnabled'), 'pos', (cp.get('XPos'), cp.get('YPos')), 'H', cp.get('Height'), 'disp', (cp.get('DisplayX'), cp.get('DisplayY'), cp.get('DisplayHeight')), 'px', (cp.get('DisplayPixelX'), cp.get('DisplayPixelY')), 'dir', cp.get('SpriteDirection'), 'progs', progs, '|', cp.get('EntityName'))
