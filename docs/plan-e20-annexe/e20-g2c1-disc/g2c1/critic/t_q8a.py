import sys, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import evlib as L
W = 52
def show(m, recs, rows_extra=2, cols=(0, 52)):
    cells = L.load_cells(m); d = L.load_map(m); si = d['SpriteInfo']
    for r in recs:
        e = si['Entities']['Entities'][r]; h = si['SpriteRecords'][e['SpriteTableIndex']]['Header']
        px = (e['XPos'] + 1) * 12; py = (e['YPos'] + 1) * 8
        col = px // 24; row = py // 16
        print('map', m, 'rec', r, 'sprite', e['SpriteTableIndex'], 'dir', e['SpriteDirection'], 'A', e['EventCodesA_LoadIndex'], 'C', e['EventCodesC_TickIndex'], 'pos px', (px, py), 'h', e['Height'], 'cell', (col, row))
        for rr in range(max(0, row - rows_extra), min(60, row + rows_extra + 1)):
            line = []
            for cc in range(max(0, col - 6), min(52, col + 9)):
                i = rr * W + cc
                w = cells['walkability'][i] | (cells['ground_property'][i] << 8)
                line.append('%04X' % w)
            print('   row %2d (cols %d..): %s' % (rr, max(0, col - 6), ' '.join(line)))
d = L.load_map(22); si = d['SpriteInfo']
for k, e in enumerate(si['Entities']['Entities']):
    if e and e['SpriteTableIndex'] in (34, 37) and (e['SpriteDirection'] & 0x80):
        print('22 rec', k, 'sprite', e['SpriteTableIndex'], 'A', e['EventCodesA_LoadIndex'], 'C', e['EventCodesC_TickIndex'], 'pos', e['XPos'], e['YPos'], e['Height'])
show(22, (21, 33))
