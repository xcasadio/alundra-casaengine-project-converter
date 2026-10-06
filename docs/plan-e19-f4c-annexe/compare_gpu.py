import json, os, sys
sys.dont_write_bytecode = True
import numpy as np
from PIL import Image
HERE = os.path.dirname(os.path.abspath(__file__))
meta = json.load(open(os.path.join(HERE, 'out', 'refs_meta.json')))
def load(p): return np.array(Image.open(p).convert('RGB'))
def diff(a, b):
    if a.shape != b.shape: return -1, 'shape %s vs %s' % (a.shape, b.shape)
    bad = np.any(a != b, axis=2); n = int(bad.sum())
    if n == 0: return 0, ''
    ys, xs = np.nonzero(bad); y, x = int(ys[0]), int(xs[0])
    return n, 'first (%d,%d) got %s want %s' % (x, y, tuple(int(v) for v in a[y, x]), tuple(int(v) for v in b[y, x]))
rows = []
for name in sorted(meta):
    g = os.path.join(HERE, 'out', 'gpu', name + '.png')
    if not os.path.exists(g): rows.append((name, 'missing')); continue
    got = load(g)
    k = int(name.rsplit('_x', 1)[1])
    res = []
    for rule in ('center', 'psx'):
        ref = load(os.path.join(HERE, 'out', 'ref', '%s_%s.png' % (name.rsplit('_x', 1)[0] + '_x1', rule)))
        if k > 1: ref = np.repeat(np.repeat(ref, k, axis=0), k, axis=1)
        res.append((rule, diff(got, ref)))
    p = meta[name]['portrait']
    rows.append((name, 'portrait=%s' % str(None if p is None else (p['x'], p['y'], p['w'], p['h'], p['rgb'])), res))
for r in rows:
    if len(r) == 2: print(r); continue
    name, ptxt, res = r
    print('%-14s %-34s center: %-8s psx: %-8s %s' % (name, ptxt, res[0][1][0], res[1][1][0], res[0][1][1] if res[0][1][0] else ''))
