"""reach2.py MAP mask ignore_h x,y... : flood fill with walkability & mask == 0 (and optionally ignoring heights), from tiles. READ-ONLY."""
import sys
sys.path.insert(0, '.')
import evlib as L
m = int(sys.argv[1]); mask = int(sys.argv[2], 0); ign = sys.argv[3] == '1'
starts = [tuple(int(a) for a in s.split(',')) for s in sys.argv[4:]]
c = L.load_cells(m); d = L.load_map(m); W = d['Map']['Width']; H = d['Map']['Height']
def free(x, y): return 0 <= x < W and 0 <= y < H and (c['walkability'][y*W+x] & mask) == 0
comps = []
for sx, sy in starts:
    comp = set(); st = [(sx, sy)]
    while st:
        x, y = st.pop()
        if (x, y) in comp or not free(x, y): continue
        comp.add((x, y))
        for dx, dy in ((1,0),(-1,0),(0,1),(0,-1)):
            nx, ny = x+dx, y+dy
            if free(nx, ny) and (ign or abs(c['height'][ny*W+nx]-c['height'][y*W+x]) <= 1):
                st.append((nx, ny))
    comps.append(comp)
    print((sx, sy), len(comp), 'contains shop cell (8,41):', (8,41) in comp, ' (5,18):', (5,18) in comp)
