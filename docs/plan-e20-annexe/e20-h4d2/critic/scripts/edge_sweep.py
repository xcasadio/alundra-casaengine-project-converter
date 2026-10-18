"""critic: re-derive the edge pairs of the column's hit box against a ClassA block (sprite 25), on my interpreter. Hero at (100,100), block at (x,y), 40 ticks, first hit tick or None."""
import sys, os
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), 'h4o'))
import fire1_lib as L
def first_hit(x, y):
    res = L.run(ticks=40, targets=[(x, y)])
    for r in res['rows']:
        if r['hits']:
            return r['t']
    return None
print('x sweep at y=100:', {x: first_hit(x, 100) for x in (75, 76, 77, 121, 122, 123)}, flush=True)
print('y sweep at x=100:', {y: first_hit(100, y) for y in (84, 85, 86, 114, 115, 116)}, flush=True)
