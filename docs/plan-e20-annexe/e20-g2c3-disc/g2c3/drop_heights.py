"""drop_heights.py - for every enabled water drop: the record height (px = Height*8), the terrain height under its (x, y) cell from the exported tilemap (AlundraCells.height, in 8 px units as the record Height; the DLL/binary sample the 4 corners
of the body, this reads the centre cell only), and the difference = how far the first fall is.   Run: python -B -I -X utf8 drop_heights.py"""
import json, glob, re, os, sys
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(HERE, 'replay', 'work', 'orc'))
import k0lib as K
R = r"D:/development/repo/alundra-casaengine-project-converter"
out = []
import collections
diffs = collections.Counter()
for n in (87, 88, 90, 92, 94, 158):
    cellf, (W_, H_, g, v) = K.cells_from_export(n)
    d = json.load(open(R + '/data-extracted/data/map_%d.json' % n, encoding='utf-8-sig'))
    si = d['SpriteInfo']
    out.append('map %d: size %dx%d cells, gravity %d, viscosity %d' % (n, W_, H_, g, v))
    for k, e in enumerate(si['Entities']['Entities']):
        if e is None or not e['IsEnabled'] or e['SpriteTableIndex'] != 135:
            continue
        r = si['SpriteRecords'][135]['Header']
        if r['ProgramTick'] != 71:
            continue
        x = e['XPos'] * 12 + 12; y = e['YPos'] * 8 + 8
        col, row = x // 24, y // 16
        c = cellf(col, row) or {}
        h = c.get('h', None)
        diff = e['Height'] - h if h is not None else None
        diffs[diff] += 1
        out.append('   rec %3d px(%4d,%4d) recH=%2d cell(%d,%d) h=%s slope=%s -> record-minus-ground = %s units (= %s px)' % (k, x, y, e['Height'], col, row, h, c.get('slope'), diff, None if diff is None else diff * 8))
out.append('histogram of (recordH - cellH) in 8 px units: %s' % dict(diffs))
print('\n'.join(out))
open(os.path.join(HERE, 'drop_heights.txt'), 'w', encoding='utf-8').write('\n'.join(out) + '\n')
