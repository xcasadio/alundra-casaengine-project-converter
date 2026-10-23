"""mapgrid.py MAP COL ROW [R] - prints a neighbourhood of the real cell field: walkability(hex) / ground property / slope / height"""
import json, sys
mp, c0, r0 = int(sys.argv[1]), int(sys.argv[2]), int(sys.argv[3]); R = int(sys.argv[4]) if len(sys.argv) > 4 else 6
d = json.load(open('D:/development/repo/alundra-casaengine-project-converter/data-extracted/data/map_%d.json' % mp, encoding='utf-8-sig'))
g = {(t['TileX'], t['TileY']): t for t in d['Map']['MapTiles']}
print('map', mp, 'cols', d['Map']['Width'], 'rows', d['Map']['Height'], ' (cell = 24 x 16 px; walkability/groundprop/slope/height)')
print('     ' + ' '.join('%-11d' % c for c in range(max(0, c0 - R), min(d['Map']['Width'], c0 + R + 1))))
for r in range(max(0, r0 - R), min(d['Map']['Height'], r0 + R + 1)):
    print('%3d  ' % r + ' '.join('%02X/%02X/%d/%-3d  ' % (g[(c, r)]['Walkability'], g[(c, r)]['GroundProperty'], g[(c, r)]['Slope'], g[(c, r)]['Height']) for c in range(max(0, c0 - R), min(d['Map']['Width'], c0 + R + 1))))
