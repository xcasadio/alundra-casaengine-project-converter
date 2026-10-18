"""Census of per-cell ground_property across the 483 exported maps (AlundraCells of alundra-project/Maps/*/*/tilemap/*.tileMap).
The slope index UpdateGroundSlope feeds Slope_18c with is (GroundProperty >> 1) & 7 (the four corners must agree).  Read-only.  python -B -I census_ground.py"""
import glob, json, os, re, collections, sys
R = 'D:/development/repo/alundra-casaengine-project-converter/'
files = sorted(glob.glob(R + 'alundra-project/Maps/*/*/tilemap/*.tileMap'))
per_value = collections.Counter()
maps_with = collections.defaultdict(set)
n = 0
for f in files:
    with open(f, encoding='utf-8') as fh:
        d = json.load(fh)
    cp = d.get('custom_properties', {})
    if 'AlundraCells' not in cp:
        continue
    cells = json.loads(cp['AlundraCells'])
    mid = cells.get('map_index')
    n += 1
    for v in cells['ground_property']:
        s = (v >> 1) & 7
        per_value[v] += 1
        maps_with[(v, s)].add(mid)
out = []
out.append('maps %d' % n)
out.append('ground_property value -> cells (slope index = (v>>1)&7)')
for v, c in sorted(per_value.items()):
    out.append('  v=%d (0x%X) slope=%d cells=%d maps=%d' % (v, v, (v >> 1) & 7, c, len(maps_with[(v, (v >> 1) & 7)])))
for key in sorted(maps_with):
    v, s = key
    if s in (1, 4):
        out.append('slope %d value %d maps: %s' % (s, v, sorted(maps_with[key])[:60]))
open('census_ground.txt', 'w').write('\n'.join(out) + '\n')
print('\n'.join(out))
