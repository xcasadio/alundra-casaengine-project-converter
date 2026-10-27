import json, sys
ROOT = r'D:/development/repo/alundra-casaengine-project-converter/data-extracted/data/tiled/'
n, x0, x1, y0, y1 = [int(a) for a in sys.argv[1:6]]
cells = json.load(open(ROOT + 'map_%d.alundra.json' % n, encoding='utf-8-sig'))['Cells']
g = {(c['X'], c['Y']): c for c in cells}
print('legend: D=deep water (GP&0xE==8), s=shallow water (GP&8), .=other ; number = height')
print('     ' + ' '.join('%2d' % x for x in range(x0, x1 + 1)))
for y in range(y0, y1 + 1):
    row = []
    for x in range(x0, x1 + 1):
        c = g[(x, y)]
        gp = c['GroundProperty']
        ch = 'D' if (gp & 0xE) == 8 else ('s' if gp & 0x8 else '.')
        row.append('%s%d' % (ch, c['Height'] % 10))
    print('%3d: ' % y + ' '.join(row))
