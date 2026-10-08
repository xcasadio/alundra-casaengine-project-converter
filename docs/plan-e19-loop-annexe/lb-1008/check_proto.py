# Compare the prototype extraction (out-lb) with the oracle and with data-extracted.
import csv, hashlib, os, sys
import numpy as np
from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = r'D:\development\repo\alundra-casaengine-project-converter'
OUT = os.path.join(HERE, 'out-lb')
ORACLE = r'C:\Users\casad\AppData\Local\Temp\claude\D--development-repo-alundra-casaengine-project-converter\b00d1a72-420d-4acc-ab34-0c25fbdad3a1\scratchpad\loop-data\oracle\sound'
TABLE = os.path.join(REPO, 'docs', 'plan-e19-loop-annexe', 'data', 'oracle_sha256.tsv')
PRED = os.path.join(REPO, 'docs', 'plan-e19-loop-annexe', 'data', 'predicted_extract_changes.txt')


def sha(p):
    h = hashlib.sha256()
    with open(p, 'rb') as f:
        for c in iter(lambda: f.read(1 << 22), b''):
            h.update(c)
    return h.hexdigest()


rows = list(csv.DictReader(open(TABLE, newline=''), delimiter='\t'))
bad = [r['File'] for r in rows if sha(os.path.join(OUT, 'sound', 'bgm', r['File'])) != r['SHA256']]
print('wav vs oracle table: mismatches', bad)
print('bgm.json proto', sha(os.path.join(OUT, 'sound', 'bgm.json')), 'oracle', sha(os.path.join(ORACLE, 'bgm.json')))
print('bgm.json byte-equal to oracle', open(os.path.join(OUT, 'sound', 'bgm.json'), 'rb').read() == open(os.path.join(ORACLE, 'bgm.json'), 'rb').read())
print('bgm_044.wav in proto', os.path.exists(os.path.join(OUT, 'sound', 'bgm', 'bgm_044.wav')))

# font3.png pixel diff
a = np.array(Image.open(os.path.join(REPO, 'data-extracted', 'ui', 'font3.png')).convert('RGBA')).astype(int)
b = np.array(Image.open(os.path.join(OUT, 'ui', 'font3.png')).convert('RGBA')).astype(int)
d = np.any(a != b, axis=2)
ys, xs = np.nonzero(d)
print('font3 size', a.shape, b.shape, 'differing texels', int(d.sum()))
print('old colours', sorted({tuple(a[y, x]) for y, x in zip(ys, xs)}), 'new colours', sorted({tuple(b[y, x]) for y, x in zip(ys, xs)}))
cells = {}
for y, x in zip(ys, xs):
    c = (y // 16) * 16 + (x // 16)
    cells[c] = cells.get(c, 0) + 1
print('cells', dict(sorted(cells.items())))
print('index-0 colour kept: transparent texels old/new', int((a[:, :, 3] == 0).sum()), int((b[:, :, 3] == 0).sum()),
      'RGB of transparent equal', bool(np.all(a[a[:, :, 3] == 0] == b[a[:, :, 3] == 0])))
print('font3.png sha256 new', sha(os.path.join(OUT, 'ui', 'font3.png')), os.path.getsize(os.path.join(OUT, 'ui', 'font3.png')))
print('mode of new png', Image.open(os.path.join(OUT, 'ui', 'font3.png')).mode, 'old', Image.open(os.path.join(REPO, 'data-extracted', 'ui', 'font3.png')).mode)
