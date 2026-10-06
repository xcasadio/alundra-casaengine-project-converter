"""Compares a folder of GPU captures (one PNG per pinned image, named like its `name` in classes.tsv, written by a probe or by a test with an environment variable of its own)
with the reference images of refs/, by the comparison class of classes.tsv (the same rules as Alundra.Tests/UI/AlundraSpeakerPixelTests.cs):
  exact : every pixel equal;
  tint8 : every pixel outside the quad of the portrait equal; inside it, each channel within 8/255, the rows and columns on a tie of the centre rule excluded.
Usage: python compare_gpu.py <folder of captures>
"""
import os
import sys

sys.dont_write_bytecode = True
import numpy as np
from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))


def load(p):
    return np.array(Image.open(p).convert('RGB')).astype(int)


def ties(n_dst, src):
    """Indices i of the destination where (2 i + 1) * src is a multiple of 2 * dst: the centre rule falls on a texel border (a float tie)."""
    return {i for i in range(n_dst) if ((2 * i + 1) * src) % (2 * n_dst) == 0}


def compare(got, want, cls, quad, k, ox, oy, src_w=48, src_h=None):
    if got.shape != want.shape:
        return 'shape %s vs %s' % (got.shape, want.shape)
    bad = np.any(got != want, axis=2)
    if cls == 'tint8' and quad != '-':
        x, y, w, h = (int(v) for v in quad.split(','))
        x0, y0, W, H = x * k + ox, y * k + oy, w * k, h * k
        inside = np.zeros(bad.shape, dtype=bool)
        inside[y0:y0 + H, x0:x0 + W] = True
        within = np.all(np.abs(got - want) <= 8, axis=2)
        skip = np.zeros(bad.shape, dtype=bool)
        for i in ties(W, src_w):
            skip[y0:y0 + H, x0 + i] = True
        for j in ties(H, src_h):
            skip[y0 + j, x0:x0 + W] = True
        bad = (bad & ~inside) | (inside & ~within & ~skip)
    n = int(bad.sum())
    if n == 0:
        return 'ok'
    ys, xs = np.nonzero(bad)
    return '%d pixels differ, first (%d, %d) got %s want %s' % (n, xs[0], ys[0], tuple(got[ys[0], xs[0]]), tuple(want[ys[0], xs[0]]))


def main(folder):
    sys.path.insert(0, HERE)
    from assets import portraits
    heights = {p['bank']: p['h'] for p in portraits()}
    rows = [ln.split('\t') for ln in open(os.path.join(HERE, 'classes.tsv'), encoding='utf-8').read().splitlines()[1:]]
    failed = 0
    for name, scen, frame, rel, k, ox, oy, rgb, cls, quad, ref in rows:
        g = os.path.join(folder, name + '.png')
        if not os.path.exists(g):
            print('%-18s missing' % name)
            failed += 1
            continue
        res = compare(load(g), load(os.path.join(HERE, 'refs', ref)), cls, quad, int(k), int(ox), int(oy), 48, 72 if scen == 'S8' else 56)
        failed += res != 'ok'
        print('%-18s %-6s rgb %-4s %s' % (name, cls, rgb, res))
    print('%d of %d images fail' % (failed, len(rows)))
    return 1 if failed else 0


if __name__ == '__main__':
    sys.exit(main(sys.argv[1]))
