"""near.py - for the maps with both drops and torches: pixel positions (x = XPos*12+12, y = YPos*8+8, z = Height*8 px) of every drop and torch, and the nearest drop-torch pairs (horizontal distance, in px)."""
import json, glob, re, math
R = r"D:/development/repo/alundra-casaengine-project-converter"
for n in (87, 88, 90, 92, 94, 158):
    d = json.load(open(R + '/data-extracted/data/map_%d.json' % n, encoding='utf-8-sig'))
    si = d['SpriteInfo']
    drops, torches = [], []
    for k, e in enumerate(si['Entities']['Entities']):
        if e is None or not e['IsEnabled']: continue
        i = e['SpriteTableIndex']
        r = si['SpriteRecords'][i] if i < len(si['SpriteRecords']) else None
        if r is None: continue
        h = r['Header']
        p = (k, e['XPos'] * 12 + 12, e['YPos'] * 8 + 8, e['Height'] * 8, e['SpriteDirection'])
        if i == 135 and h['ProgramTick'] == 71: drops.append(p)
        if i == 137 and h['ProgramTick'] == 70: torches.append(p)
    print('map %d: %d drops, %d torches' % (n, len(drops), len(torches)))
    for p in drops: print('   drop  rec %3d px(%4d,%4d) z=%3d dir=%d' % p)
    for p in torches: print('   torch rec %3d px(%4d,%4d) z=%3d dir=%d' % p)
    pairs = sorted((math.hypot(a[1]-b[1], a[2]-b[2]), a[0], b[0]) for a in drops for b in torches)
    print('   nearest drop-torch pairs (dist px, drop rec, torch rec):', [(round(x), a, b) for x, a, b in pairs[:6]])
