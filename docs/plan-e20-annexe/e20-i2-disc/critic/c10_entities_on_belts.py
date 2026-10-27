"""Critic: placed Entities of the belt maps standing on a belt cell. Two readings of the cell: (A) object x,y (pixels, top-left) -> (x//24, y//16);
(B) XPos/YPos (units of 12 and 8 px, i.e. half cells) -> (XPos//2, YPos//2). Reports per map the entities on a belt cell, with the gate/lock/direction."""
import json, glob, re, os, collections
MAPS = r'D:/development/repo/alundra-casaengine-project-converter/alundra-project/Maps'
ROOT = r'D:/development/repo/alundra-casaengine-project-converter/data-extracted/data/tiled/'
BELTMAPS = [0,3,4,5,6,8,10,15,19,20,23,49,77,78,79,97,100,116,117,118,124,125,127,128,151,157,271,289,293,297,301,324,331,335,351,357,403,404,405,416,434,435,479,481]
files = {}
for p in glob.glob(MAPS + '/*/*/tilemap/*.tileMap'):
    m = re.search(r'-(\d+)\.tileMap$', p)
    if m: files[int(m.group(1))] = p
resA = collections.OrderedDict(); resB = collections.OrderedDict(); det = []
for n in BELTMAPS:
    cells = json.load(open(ROOT + 'map_%d.alundra.json' % n, encoding='utf-8-sig'))['Cells']
    g = {(c['X'], c['Y']): c for c in cells}
    if n not in files: print('no tileMap for', n); continue
    d = json.load(open(files[n], encoding='utf-8-sig'))
    ents = [o for ol in d['object_layers'] if ol['name'] == 'Entities' for o in ol['objects']]
    a = b = 0
    for o in ents:
        cp = o['custom_properties']
        ca = g.get((int(o['x'] // 24), int(o['y'] // 16)))
        cb = g.get(((int(cp["XPos"]) * 12 + 12) // 24, (int(cp["YPos"]) * 8 + 8) // 16))
        if ca and ca['Slope'] & 0xC0: a += 1
        if cb and cb['Slope'] & 0xC0:
            b += 1
            s = cb['Slope']
            det.append('map %d %s (cell %d,%d) sprite %s slope %02x lock=%d gate=%d%d h=%d entH=%s' % (n, o['name'], cb['X'], cb['Y'], cp.get('SpriteTableIndex'), s, (s >> 7) & 1, (s >> 3) & 1, (s >> 2) & 1, cb['Height'], cp.get('Height')))
    resA[n] = (len(ents), a); resB[n] = (len(ents), b)
print('A (object x,y):', {n: v[1] for n, v in resA.items() if v[1]}, 'maps', sum(1 for v in resA.values() if v[1]), 'ents', sum(v[1] for v in resA.values()))
print('B (XPos/YPos): ', {n: v[1] for n, v in resB.items() if v[1]}, 'maps', sum(1 for v in resB.values() if v[1]), 'ents', sum(v[1] for v in resB.values()))
print('\n'.join(det))
