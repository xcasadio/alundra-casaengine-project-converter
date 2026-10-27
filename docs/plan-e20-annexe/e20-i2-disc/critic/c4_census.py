import json, os, collections
ROOT = r'D:/development/repo/alundra-casaengine-project-converter/data-extracted/data/tiled'
tot = collections.Counter(); maps = {}
for n in range(483):
    p = os.path.join(ROOT, 'map_%d.alundra.json' % n)
    if not os.path.exists(p): continue
    cells = json.load(open(p, encoding='utf-8-sig'))['Cells']
    c = collections.Counter()
    for k in cells:
        s = k['Slope']
        if s & 0xC0:
            c['belt'] += 1
            deep = (k['GroundProperty'] & 0xE) == 8
            c['deep' if deep else 'dry'] += 1
            if s & 0x80: c['lock'] += 1
            g = (s >> 2) & 3
            c['gate%d' % g] += 1
    if c['belt']:
        maps[n] = c
        tot.update(c)
print('maps with belt', len(maps), 'total', dict(tot))
print('maps with deep belt', sorted(n for n, c in maps.items() if c['deep']), len([n for n, c in maps.items() if c['deep']]))
print('maps with lock', sorted(n for n, c in maps.items() if c['lock']), len([n for n, c in maps.items() if c['lock']]))
# level-gate cells with neither 0x04 nor 0x08 : belt that never fires with gravity
print('gate0 belt cells (never active under gravity):', tot['gate0'])
