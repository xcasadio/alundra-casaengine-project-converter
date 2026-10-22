"""Static facts of the entities an sm1 arc touches: the sprite record (flags, box) of a plate and of an object, from alundra-project/Data/sprite-records.json.
Usage: python -B arc_entities.py MAP REC [REC ...]"""
import json, glob, sys, os
ROOT = 'D:/development/repo/alundra-casaengine-project-converter/alundra-project'
BS = chr(92)
recs = json.load(open(ROOT + '/Data/sprite-records.json', encoding='utf-8'))
idx = json.load(open(ROOT + '/Maps/world-index.json', encoding='utf-8'))


def tilemap_of(m):
    rel = idx[str(m)].replace(BS, '/')
    folder = os.path.dirname(ROOT + '/' + rel)
    return glob.glob(folder + '/tilemap/*.tileMap')[0]


if __name__ == '__main__':
    m = int(sys.argv[1])
    want = [int(x) for x in sys.argv[2:]]
    d = json.load(open(tilemap_of(m), encoding='utf-8'))
    for layer in d['object_layers']:
        if layer['name'] != 'Entities':
            continue
        for o in layer['objects']:
            cp = o['custom_properties']
            i = int(cp['Index'])
            if want and i not in want:
                continue
            r = recs.get(cp.get('PrefabAssetId'))
            if r is None:
                print(i, 'no sprite record', cp.get('PrefabAssetId'))
                continue
            flags = r['MoreFlags'] | (r['CanPickup'] << 8) | (r['FlagsPortraitShadowType'] << 16)
            keys = [k for k in r if k.lower() in ('width', 'height', 'depth', 'offsetz', 'modz', 'bodywidth', 'bodyheight', 'bodydepth', 'boxwidth', 'boxheight', 'boxdepth')]
            print(i, 'sprite', cp.get('SpriteTableIndex'), 'flags 0x%X' % flags, {k: r[k] for k in keys}, 'object pos', (o.get('x'), o.get('y')), 'XPos/YPos', (cp.get('XPos'), cp.get('YPos')), 'Height', cp.get('Height'))
        break
    print('record keys sample:', sorted(r.keys()))
