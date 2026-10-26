"""firenear.py (critic): in the maps that hold brambles (global sprites 238/239), the placements of sprite indices whose balance record holds an attack with nibble 6, and the
Chebyshev distance (placement units) to the nearest bramble. Map-page index = SpriteTableIndex when the map's SpriteTable[idx] != -1 (then type = 256 + idx), else global (type = idx)."""
import json, glob, re, collections
D = "D:/development/repo/alundra-casaengine-project-converter/data-extracted/data"
bal = json.load(open("D:/development/repo/alundra-casaengine-project-converter/alundra-project/Data/balance.json"))['EntriesBySpriteIndex']
fire = set()
for t, e in enumerate(bal):
    if e is None: continue
    for a in (e.get('Attacks') or []):
        if a['AttackAttribute'] and (a['AttackAttribute'] & 0xF) == 6:
            fire.add(t)
fire -= {238, 239}
out = []
for f in sorted((p for p in glob.glob(D + '/map_*.json') if re.search(r'map_\d+\.json$', p)), key=lambda p: int(re.search(r'map_(\d+)\.json', p).group(1))):
    n = int(re.search(r'map_(\d+)\.json', f).group(1))
    d = json.load(open(f, encoding='utf-8-sig'))
    si = d['SpriteInfo']; st = si['SpriteTable']
    br = []; fi = []
    for k, e in enumerate(si['Entities']['Entities']):
        if e is None or not e['IsEnabled']: continue
        i = e['SpriteTableIndex']
        mp = i < len(st) and st[i] != -1
        typ = 256 + i if mp else i
        if (not mp) and i in (238, 239): br.append((e['XPos'], e['YPos'], e['Height']))
        elif typ in fire: fi.append((typ, k, e['XPos'], e['YPos'], e['Height']))
    if br and fi:
        best = None
        for (t, k, x, y, h) in fi:
            for (bx, by, bh) in br:
                dist = max(abs(x - bx), abs(y - by))
                if best is None or dist < best[0]: best = (dist, t, k, (x, y, h), (bx, by, bh))
        out.append('map %3d: brambles %2d, fire placements %2d (types %s), nearest: %s' % (n, len(br), len(fi), sorted({t for t, *_ in fi}), best))
print('\n'.join(out))
