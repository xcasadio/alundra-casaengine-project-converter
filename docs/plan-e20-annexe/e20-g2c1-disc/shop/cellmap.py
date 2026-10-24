"""cellmap.py MAP x0 x1 y0 y1 : print height/walkability grid of cells (index = y*W + x), W from the map. READ-ONLY."""
import sys
sys.path.insert(0, '.')
import evlib as L
m = int(sys.argv[1]); x0, x1, y0, y1 = [int(a) for a in sys.argv[2:6]]
c = L.load_cells(m); d = L.load_map(m)
W = d['Map']['Width']; H = d['Map']['Height']
print('W', W, 'H', H, 'cells', c['cell_count'])
print('height (tile z units of 16px = height value?) and walkability hex')
print('    ' + ''.join('%7d' % x for x in range(x0, x1 + 1)))
for y in range(y0, y1 + 1):
    row = ''
    for x in range(x0, x1 + 1):
        i = y * W + x
        row += ' %2d/%02X ' % (c['height'][i], c['walkability'][i] & 0xFF)
    print('%3d ' % y + row)
