#!/usr/bin/env python3
"""critic-complet c9 (c8b over every map with liftables; NONWIRED=1 keeps only the non-C23 headers solid: C4 stones, barrels, logs, the four objects, torches): COARSE cell reachability (INFERENCE tool, not the game's physics) on the chain maps with liftables: does making the
liftables solid (g3) cut an exit (portal) off an arrival, (a) walking only, (b) walking plus a jump of one height unit (16 px)?

Model (stated, not measured):
- grid of 24 x 16 px cells from data-extracted/data/map_<N>.json (Map.MapTiles); a cell is a wall when Walkability & 0x41 != 0
  (the legend of ../bin/map392_ascii.py); Height in units of 16 px (392: height 4 = z 64, ../bin/report.md A3.1);
- a liftable footprint (../dll/all_records.json: box [pos+off, pos+off+size), z = 8 * Height) raises every cell it overlaps to
  max(cell floor, crate top), in 16 px units (a 21 x 15 box leaves at most 14 px of a 24 x 16 cell: the 21 px hero cannot pass beside it);
- moves between 4-neighbours: down any amount (falling), up by at most U units (U = 0 walking, U = 1 with the jump: apex 27.5 px,
  plan-e20-heros.md 6281); a cell with Slope 6 or 7 (the two non-default values) also allows +/-1 unit both ways (ramp);
- liftables NOT spawned at the map load (SpriteDirection & 0x40 == 0 or IsEnabled == 0, EntitySpawnFactory.cs:84-99) are left out;
- arrivals: the destination tiles of every portal of the export into the map, plus the 392 B1 placement (720, 216); exits: every portal
  rectangle (X1..X2, Y1..Y2) of the map.
Control case: map 392 must come out closed walking and open with the jump (the binary: the jump over rec5 passes, ../bin/report.md A3.5).
Read-only, stdout utf-8. Usage: c8_reach.py [MAP ...] (default: the 18 chain maps with liftables)."""
import glob, json, os, re, sys
from collections import deque, defaultdict
sys.stdout.reconfigure(encoding='utf-8')
HERE = os.path.dirname(os.path.abspath(__file__))
REPO = 'D:/development/repo/alundra-casaengine-project-converter'
DATA = REPO + '/data-extracted/data'
CHAIN_LIFT = [390, 392, 163, 165, 164, 172, 170, 171, 173, 174, 175, 179, 177, 178, 180, 181, 182, 184]
NONWIRED = os.environ.get('NONWIRED') == '1'
NONWIRED_BANKS = {'34', '37', '117', '107', '111', '112', '113', '137'}  # Tonneau, Pierre, Tronc, 4 objects, Torche (c3_lift_by_name.out)
recs = json.load(open(os.path.join(HERE, '..', 'dll', 'all_records.json'), encoding='utf-8'))

# spawn gate from the exported tileMaps
gate = {}
for f in glob.glob(REPO + '/alundra-project/Maps/*/*/tilemap/*.tileMap'):
    m = re.search(r'-(\d+)\.tileMap$', os.path.basename(f))
    if not m:
        continue
    mp = int(m.group(1))
    d = json.load(open(f, encoding='utf-8'))
    for l in d['object_layers']:
        if l['name'] == 'Entities':
            for o in l['objects']:
                p = o['custom_properties']
                gate[(mp, int(p['Index']))] = int(p.get('IsEnabled', 1)) != 0 and (int(p.get('SpriteDirection', 0)) & 0x40) != 0

# arrivals from every map's portals
arrivals = defaultdict(set)
for f in glob.glob(DATA + '/map_*.json'):
    m = re.search(r'map_(\d+)\.json$', f)
    if not m:
        continue
    d = json.load(open(f, encoding='utf-8-sig'))
    for p in d['Info'].get('Portals') or []:
        arrivals[p['DestMapId']].add((p['DestTileX'], p['DestTileY'], 'portal %s.%d' % (m.group(1), (d['Info']['Portals']).index(p))))
arrivals[392].add((720 // 24, 216 // 16, 'B1 0x64 (720,216)'))


def load(mp):
    d = json.load(open('%s/map_%d.json' % (DATA, mp), encoding='utf-8-sig'))
    cells = {(c['TileX'], c['TileY']): c for c in d['Map']['MapTiles']}
    return d, cells


def raised(mp, cells):
    top = {}
    for r in recs:
        if r['map'] != mp or not r['kind'] or not gate.get((mp, r['idx']), True):
            continue
        if NONWIRED and r['bank'] not in NONWIRED_BANKS:
            continue
        x0, y0 = r['x'] + r['offx'], r['y'] + r['offy']
        x1, y1 = x0 + r['sx'], y0 + r['sy']
        t = (r['z'] + r['offz'] + r['sz']) / 16.0
        for cx in range(int(x0 // 24), int((x1 - 1) // 24) + 1):
            for cy in range(int(y0 // 16), int((y1 - 1) // 16) + 1):
                top[(cx, cy)] = max(top.get((cx, cy), 0), t)
    return top


def reach(cells, top, start, up):
    def h(c):
        return max(cells[c]['Height'], top.get(c, 0)) if top is not None else cells[c]['Height']
    if start not in cells or cells[start]['Walkability'] & 0x41:
        return None
    seen = {start}
    q = deque([start])
    while q:
        a = q.popleft()
        for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
            b = (a[0] + dx, a[1] + dy)
            if b in seen or b not in cells or cells[b]['Walkability'] & 0x41:
                continue
            ha, hb = h(a), h(b)
            ramp = cells[a]['Slope'] in (6, 7) or cells[b]['Slope'] in (6, 7)
            if hb <= ha + up or (ramp and abs(hb - ha) <= 1):
                seen.add(b)
                q.append(b)
    return seen


maps = [int(a) for a in sys.argv[1:]] or sorted({r['map'] for r in recs if r['kind']})
for mp in maps:
    d, cells = load(mp)
    top = raised(mp, cells)
    exits = []
    for i, p in enumerate(d['Info'].get('Portals') or []):
        if p['X2'] < p['X1'] or p['Y2'] < p['Y1'] or (p['DestMapId'] == 0 and p['X1'] == 0 and p['Y1'] == 0 and p['Flags'] == 0):
            continue
        exits.append((i, {(x, y) for x in range(p['X1'], p['X2'] + 1) for y in range(p['Y1'], p['Y2'] + 1)}, p['DestMapId']))
    if not top:
        continue
    print('== map %d : %d raised cells, %d exits, %d arrivals' % (mp, len(top), len(exits), len(arrivals[mp])))
    for (ax, ay, why) in sorted(arrivals[mp]):
        res = {}
        cellsets = {}
        for label, t, up in (('free', None, 0), ('walk', top, 0), ('jump', top, 1)):
            s = reach(cells, t, (ax, ay), up)
            cellsets[label] = s
            res[label] = None if s is None else {i for i, ex, _ in exits if ex & s}
        if res['free'] is None:
            print('  arrival %-16s tile (%d,%d): start cell is a wall in this model' % (why, ax, ay))
            continue
        lost_walk = sorted(res['free'] - res['walk'])
        lost_jump = sorted(res['free'] - res['jump'])
        flag = '' if not lost_walk and not lost_jump else '  <-- '
        lw = sorted(c for c in cellsets['free'] - (cellsets['walk'] or set()) if c not in top)
        lj = sorted(c for c in cellsets['free'] - (cellsets['jump'] or set()) if c not in top)
        flag += '  floor cells lost (crate cells excluded): walking %d %s ; with jump %d %s' % (len(lw), lw[:12], len(lj), lj[:12])
        print('  arrival %-16s tile (%d,%d): exits free=%s ; lost walking=%s ; lost with jump=%s%s' % (
            why, ax, ay, sorted(res['free']), lost_walk, lost_jump, flag))
