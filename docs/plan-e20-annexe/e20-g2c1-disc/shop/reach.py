"""reach.py MAP x,y [x,y...] : 4-connected flood fill over free cells (walkability & 0x41 == 0, |dh| <= 1 between neighbours) from arrival tiles;
prints the component size and whether given probe tiles are in it. Over-approximation of what the hero can reach on foot. READ-ONLY."""
import sys
sys.path.insert(0, '.')
import evlib as L
m = int(sys.argv[1]); starts = [tuple(int(a) for a in s.split(',')) for s in sys.argv[2:]]
c = L.load_cells(m); d = L.load_map(m); W = d['Map']['Width']; H = d['Map']['Height']
def free(x, y):
    if not (0 <= x < W and 0 <= y < H): return False
    return (c['walkability'][y*W+x] & 0x41) == 0
seen = {}
for si, (sx, sy) in enumerate(starts):
    comp = set(); stack = [(sx, sy)]
    while stack:
        x, y = stack.pop()
        if (x, y) in comp or not free(x, y): continue
        comp.add((x, y))
        for dx, dy in ((1,0),(-1,0),(0,1),(0,-1)):
            nx, ny = x+dx, y+dy
            if free(nx, ny) and abs(c['height'][ny*W+nx] - c['height'][y*W+x]) <= 1 and (nx, ny) not in comp:
                stack.append((nx, ny))
    xs = [p[0] for p in comp]; ys = [p[1] for p in comp]
    print('start', (sx, sy), 'free' if free(sx, sy) else 'NOT FREE', 'component', len(comp), 'bbox', (min(xs), min(ys), max(xs), max(ys)) if comp else None)
    seen[si] = comp
import itertools
for a, b in itertools.combinations(range(len(starts)), 2):
    print('components', starts[a], starts[b], 'same' if seen[a] & seen[b] else 'disjoint')
