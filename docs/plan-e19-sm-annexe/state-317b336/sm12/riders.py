import json, glob, sys, os, collections
ROOT = 'D:/development/repo/alundra-casaengine-project-converter/alundra-project'
recs = json.load(open(ROOT + '/Data/sprite-records.json', encoding='utf-8'))
k0 = next(iter(recs))
print('sample record keys:', list(recs[k0].keys())[:12] if isinstance(recs[k0], dict) else recs[k0])
idx = json.load(open(ROOT + '/Maps/world-index.json', encoding='utf-8'))


def tilemap_of(m):
    rel = idx[str(m)].replace('\\', '/')
    folder = os.path.dirname(ROOT + '/' + rel)
    return glob.glob(folder + '/tilemap/*.tileMap')[0]


def flags_of(pref):
    r = recs.get(pref)
    if not r:
        return None
    for k in ('Flags', 'flags', 'EntityFlags'):
        if k in r:
            return r[k]
    return r


def show(m, wanted=None):
    d = json.load(open(tilemap_of(m), encoding='utf-8'))
    out = []
    for layer in d['object_layers']:
        if layer['name'] != 'Entities':
            continue
        for o in layer['objects']:
            cp = o['custom_properties']
            i = int(cp['Index'])
            if wanted is not None and i not in wanted:
                continue
            pref = cp.get('PrefabAssetId')
            fl = flags_of(pref)
            out.append((i, cp.get('EntityName'), cp.get('SpriteTableIndex'), fl))
    return out


if __name__ == '__main__':
    m = int(sys.argv[1])
    wanted = set(int(x) for x in sys.argv[2:]) or None
    for i, n, s, fl in show(m, wanted):
        if isinstance(fl, str):
            try:
                v = int(fl, 0)
            except Exception:
                v = None
        elif isinstance(fl, int):
            v = fl
        else:
            v = None
        if v is not None:
            print(i, n, 'sprite', s, 'flags 0x%X' % v, 'collidable' if v & 0x80 else '-', 'grid(0x4100)==0x100' if (v & 0x4100) == 0x100 else 'NOT grid', 'gravity' if v & 0x100 else '', 'noriders' if v & 0x4000 else '')
        else:
            print(i, n, 'sprite', s, 'flags', fl)
