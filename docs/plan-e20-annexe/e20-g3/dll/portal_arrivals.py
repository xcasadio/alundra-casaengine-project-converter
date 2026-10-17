#!/usr/bin/env python3
"""Static check of D-E19-36 for the portals: does the arrival box of a portal overlap a liftable footprint (X and Y) of the destination map?

Arrival = centre of the destination tile: X = DestTileX*24+12, Y = DestTileY*16+8 (checked on portal 162.3 -> map 165 (12,22) -> (300,360),
Alundra.Tests/AlundraEntityContactArcTests.cs:432-433 and AlundraInoaDayOneArcTests.cs:104-107). Hero box: [X-10, X+11) x [Y-7, Y+8), Z [z, z+32).
The Z of the arrival is ZLevel<<20 raised to the ground by ClampToGround (not computed here): an XY overlap is reported with the Z ranges of the
liftable so that "Z overlaps?" can be read: the hero Z is unknown, so each XY overlap is 'a mesurer' unless the liftable top is below ZLevel*16.
Usage: portal_arrivals.py  -> prints every portal (any map) whose destination (map, tile) overlaps a liftable of the destination in XY.
"""
import glob, json, os, re

ROOT = 'D:/development/repo/alundra-casaengine-project-converter/alundra-project'
rows = json.load(open('all_records.json', encoding='utf-8'))
lifts = {}
for r in rows:
    if r['kind'] != 0:
        lifts.setdefault(r['map'], []).append(r)

portals = []
for f in glob.glob(ROOT + '/Maps/*/*/tilemap/*.tileMap'):
    f = f.replace(chr(92), '/')
    m = re.search(r'-(\d+)\.tileMap$', os.path.basename(f))
    if not m:
        continue
    src = int(m.group(1))
    d = json.load(open(f, encoding='utf-8'))
    for l in d['object_layers']:
        if l['name'] != 'Portals':
            continue
        for o in l['objects']:
            p = o['custom_properties']
            try:
                portals.append((src, int(p['Index']), int(p['DestMapId']), int(p['DestTileX']), int(p['DestTileY']), int(p.get('ZLevel', 0))))
            except KeyError:
                pass

print('portals in the export:', len(portals))
into_lift = [p for p in portals if p[2] in lifts]
print('portals whose destination map has liftables:', len(into_lift), 'on', len({p[2] for p in into_lift}), 'maps')
hits = []
for (src, idx, dst, tx, ty, zl) in into_lift:
    ax, ay = tx * 24 + 12, ty * 16 + 8
    hx0, hx1, hy0, hy1 = ax - 10, ax + 11, ay - 7, ay + 8
    for r in lifts[dst]:
        x0 = r['x'] + r['offx']; x1 = x0 + r['sx']
        y0 = r['y'] + r['offy']; y1 = y0 + r['sy']
        if hx0 < x1 and x0 < hx1 and hy0 < y1 and y0 < hy1:
            hits.append((src, idx, dst, (ax, ay), zl, r['idx'], r['name'], (r['z'] + r['offz'], r['z'] + r['offz'] + r['sz'])))
print('XY overlaps of an arrival with a liftable footprint:', len(hits))
for h in hits:
    print('  portal %d.%d -> map %d arrival (%d,%d) ZLevel=%d overlaps rec%d %s z[%d,%d)' % (h[0], h[1], h[2], h[3][0], h[3][1], h[4], h[5], h[6][:12], h[7][0], h[7][1]))
chain = [389, 390, 476, 478, 392, 391, 416, 163, 162, 165, 164, 172, 169, 170, 171, 173, 174, 175, 10, 179, 176, 177, 178, 180, 181, 182, 135, 183, 184, 185]
print()
print('portals INTO a chain map with liftables (arrival tile, nearest liftable gap in XY):')
for (src, idx, dst, tx, ty, zl) in sorted(into_lift, key=lambda p: (p[2], p[0], p[1])):
    if dst not in chain:
        continue
    ax, ay = tx * 24 + 12, ty * 16 + 8
    best = None
    for r in lifts[dst]:
        x0 = r['x'] + r['offx']; x1 = x0 + r['sx']
        y0 = r['y'] + r['offy']; y1 = y0 + r['sy']
        gx = max(x0 - (ax + 11), (ax - 10) - x1, 0)
        gy = max(y0 - (ay + 8), (ay - 7) - y1, 0)
        g = max(gx, gy)
        if best is None or g < best[0]:
            best = (g, r['idx'])
    print('  %d.%d -> %d tile(%d,%d) = (%d,%d) ZLevel=%d : nearest liftable rec%d gap=%d px' % (src, idx, dst, tx, ty, ax, ay, zl, best[1], best[0]))
