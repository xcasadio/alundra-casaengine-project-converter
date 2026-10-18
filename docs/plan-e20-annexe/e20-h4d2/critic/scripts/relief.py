import sys, os
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), 'h4o'))
import fire1_lib as L
def lives(res):
    seen = {}
    for r in res['rows']:
        for s, e in r['ents'].items():
            if e['type'] != 23: continue
            k = (s, e['id'])
            v = seen.setdefault(k, dict(first=r['t'], st3=None, last=r['t'], dir=e['dir_t'], z3=None, x3=None, y3=None))
            if e['status'] == 3 and v['st3'] is None:
                v['st3'] = r['t']; v['z3'] = round(e['z'] / 65536, 1); v['x3'] = round(e['x'] / 65536, 1); v['y3'] = round(e['y'] / 65536, 1)
            v['last'] = r['t']
    return seen
base = lives(L.run(ticks=200))
def cmp(name, cells, hero=(100, 100)):
    res = L.run(ticks=200, cells=cells, hero=hero)
    s = lives(res)
    nb = sorted(s.values(), key=lambda v: v['first'])
    bb = sorted(base.values(), key=lambda v: v['first'])
    diff = [(i, b['st3'], n['st3'], n['z3'], n['x3'], n['y3']) for i, (b, n) in enumerate(zip(bb, nb)) if (b['st3'], b['last']) != (n['st3'], n['last'])]
    print(name, 'balls', len(nb), 'balls whose life differs from the flat key:', len(diff), 'e.g. (idx, flat st3, new st3, z, x, y):', diff[:5], flush=True)
def plateau(c0, c1, hb): return L.cells_cols(c0, c1, (hb, 0, 0, 0))
cmp('plateau hb=1 (16px) cols 6..12', plateau(6, 12, 1))
cmp('plateau hb=2 (32px) cols 6..12', plateau(6, 12, 2))
cmp('ledge hb=2 col 6 only', plateau(6, 6, 2))
def hero_plateau(col, row):
    return (2, 0, 0, 0) if (col <= 5 and 2 <= row <= 6) else (0, 0, 0, 0)
cmp('hero on a plateau hb=2 (cols<=5, rows 2..6), ground lower elsewhere', hero_plateau)
