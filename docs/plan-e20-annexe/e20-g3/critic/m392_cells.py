"""critic: ASCII of map 392 cells (data-extracted Map.MapTiles): each cell printed as height digit, '#' when Walkability != 0
(with the walkability values listed), crates and portal marked.  Plus a 4-neighbour flood fill over cells with Walkability == 0 and the same
height as the start cell, from the hero arrival cell (30, 13) = (720, 216), to see whether the portal cell (22, 23) is reachable WITHOUT the crates
cells (crate cells treated as blocked) - a coarse cell-level check only (the real test is per pixel).  Read-only."""
import json
from collections import deque, Counter

D = json.load(open(r"D:/development/repo/alundra-casaengine-project-converter/data-extracted/data/map_392.json", encoding="utf-8-sig"))
T = {(c['TileX'], c['TileY']): c for c in D['Map']['MapTiles']}
W, H = D['Map']['Width'], D['Map']['Height']
print('map', W, 'x', H, 'walkability values', Counter(c['Walkability'] for c in T.values()), 'heights', Counter(c['Height'] for c in T.values()))
recs = [e for e in D['SpriteInfo']['Entities']['Entities'] if e]
crate_cells = set()
for e in recs:
    x, y = 12 * e['XPos'] + 12, 8 * e['YPos'] + 8
    for px in (x - 10, x + 10):
        for py in (y - 7, y + 7):
            crate_cells.add((px // 24, py // 16))
for row in range(0, 40):
    line = ''
    for col in range(14, 40):
        c = T[(col, row)]
        ch = '#' if c['Walkability'] else str(c['Height'] % 10)
        if (col, row) == (22, 23):
            ch = 'P'
        elif (col, row) in crate_cells:
            ch = 'c' if not c['Walkability'] else 'C'
        line += ch
    print('%2d %s' % (row, line))
print('cols 14..39 ; # = Walkability != 0 ; c = crate footprint ; P = portal cell (22, 23)')


def flood(blocked):
    start = (30, 13)
    h0 = T[start]['Height']
    seen = {start}
    q = deque([start])
    while q:
        a = q.popleft()
        for d in ((1, 0), (-1, 0), (0, 1), (0, -1)):
            b = (a[0] + d[0], a[1] + d[1])
            if b in seen or b not in T or b in blocked:
                continue
            c = T[b]
            if c['Walkability'] or c['Height'] != h0:
                continue
            seen.add(b)
            q.append(b)
    return seen


s1 = flood(set())
s2 = flood(crate_cells)
print('portal reachable at cell level without crates:', (22, 23) in s1, '; with crate cells blocked:', (22, 23) in s2)
cut = [b for b in s1 if b not in s2]
print('cells lost when crate cells are blocked:', len(cut))
