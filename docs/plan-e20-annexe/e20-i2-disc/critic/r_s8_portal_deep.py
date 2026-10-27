"""s8_portal_deep.py - READ-ONLY census: portals whose destination tile (DestTileX, DestTileY of the Portals object layer of the exported .tileMap) is a deep-water cell
((GroundProperty & 0xE) == 8) of the destination map; and the placed Entities / Portals of every belt map standing on a belt cell (x,y only).  Writes out_portal_deep.txt."""
import json, glob, re, os, collections
MAPS = r'D:/development/repo/alundra-casaengine-project-converter/alundra-project/Maps'
ROOT = r'D:/development/repo/alundra-casaengine-project-converter/data-extracted/data/tiled/'
OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), 'out_portal_deep.txt')
cache = {}


def grid(n):
    if n not in cache:
        try:
            cells = json.load(open(ROOT + 'map_%d.alundra.json' % n, encoding='utf-8-sig'))['Cells']
            cache[n] = {(c['X'], c['Y']): c for c in cells}
        except Exception:
            cache[n] = None
    return cache[n]


lines = []
bad = []
tot = 0
for p in sorted(glob.glob(MAPS + '/*/*/tilemap/*.tileMap')):
    m = re.search(r'-(\d+)\.tileMap$', p)
    if not m:
        continue
    src = int(m.group(1))
    d = json.load(open(p, encoding='utf-8-sig'))
    for ol in d['object_layers']:
        if ol['name'] != 'Portals':
            continue
        for o in ol['objects']:
            cp = o['custom_properties']
            try:
                dm, dx, dy = int(cp['DestMapId']), int(cp['DestTileX']), int(cp['DestTileY'])
            except Exception:
                continue
            tot += 1
            g = grid(dm)
            c = g.get((dx, dy)) if g else None
            if c and (c['GroundProperty'] & 0xE) == 8:
                bad.append((src, o['name'], dm, dx, dy, c['Height']))
lines.append('portals scanned %d ; destination tile on a deep-water cell: %d (%.1f %%)' % (tot, len(bad), 100.0 * len(bad) / tot))
by_dest = collections.Counter(b[2] for b in bad)
lines.append('by destination map: %s' % dict(sorted(by_dest.items())))
for b in bad:
    lines.append('  map %d %s -> map %d tile (%d,%d) height %d' % b)
open(OUT, 'w').write('\n'.join(lines) + '\n')
print('\n'.join(lines[:3]))
