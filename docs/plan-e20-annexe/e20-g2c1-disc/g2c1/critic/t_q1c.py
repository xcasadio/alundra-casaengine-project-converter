import sys, os, collections
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import evlib as L
W = 52
tot = collections.Counter(); bytype = collections.Counter(); bymap = collections.Counter()
for m in L.all_map_ids():
    cells = L.load_cells(m)
    if cells is None: continue
    d = L.load_map(m); si = d.get('SpriteInfo')
    if not si: continue
    recs = si['SpriteRecords']
    for k, e in enumerate(si['Entities']['Entities']):
        if e is None or not e['IsEnabled']: continue
        i = e['SpriteTableIndex']
        if (e['SpriteDirection'] & 0x80) == 0: continue   # map page only
        if i >= len(recs) or recs[i] is None: continue
        h = recs[i]['Header']
        fl = h['MoreFlags'] | (h['CanPickup'] << 8) | (h['FlagsPortraitShadowType'] << 16)
        if not (fl & 0x100): continue
        px = (e['XPos'] + 1) * 12; py = (e['YPos'] + 1) * 8
        x1 = px + h['OffsetX']; y1 = py + h['OffsetY']; x2 = x1 + h['SizeX']; y2 = y1 + h['SizeY']
        mn = 0xE00
        for (x, y) in ((x1, y1), (x2, y1), (x1, y2), (x2, y2)):
            col = max(0, min(51, x // 24)); row = max(0, min(59, y // 16))
            ii = row * W + col
            w = cells['walkability'][ii] | (cells['ground_property'][ii] << 8)
            mn = min(mn, w & 0xE00)
        sl = mn >> 9
        tot['grav'] += 1
        if sl == 4:
            tot['on_water'] += 1
            kind = 'DSL' if fl & 0x100000 else 'noDSL'
            bytype[(0x100 + i, kind)] += 1; bymap[m] += 1
print(dict(tot))
print(sorted(bytype.items(), key=lambda x: -x[1])[:15])
print(sorted(bymap.items(), key=lambda x: -x[1])[:12])
