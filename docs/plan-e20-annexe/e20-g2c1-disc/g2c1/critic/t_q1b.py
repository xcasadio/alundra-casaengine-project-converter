import sys, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import evlib as L
W = 52
def corners(m, rec):
    cells = L.load_cells(m)
    d = L.load_map(m); si = d['SpriteInfo']; e = si['Entities']['Entities'][rec]
    h = si['SpriteRecords'][e['SpriteTableIndex']]['Header']
    px = (e['XPos'] + 1) * 12; py = (e['YPos'] + 1) * 8
    x1 = px + h['OffsetX']; y1 = py + h['OffsetY']
    x2 = x1 + h['SizeX']; y2 = y1 + h['SizeY']
    out = []
    for (x, y) in ((x1, y1), (x2, y1), (x1, y2), (x2, y2)):
        col = max(0, min(51, x // 24)); row = max(0, min(59, y // 16))
        i = row * W + col
        w = cells['walkability'][i] | (cells['ground_property'][i] << 8)
        out.append((col, row, '0x%04X' % w, cells['slope'][i], cells['height'][i]))
    return (px, py, e['Height']), out
for m, recs in ((70, (8,9,10,11,12,21,15,16)), (73, (10,11,12,36,37))):
    for r in recs:
        print(m, r, *corners(m, r))
