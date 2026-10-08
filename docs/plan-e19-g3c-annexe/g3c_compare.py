"""E19.g G3c: compare a back-buffer dump of the G3c harness with the prediction of g3c_model.py (same rule as g2e_compare.py).

Usage:  python g3c_compare.py <dump.bin> --scene <name> [--report out.txt] [--diff-png out.png]
        python g3c_compare.py --selftest
The dump is the one of PsxFreeQuadDemo (int32 width, int32 height, int32 k, then width * height RGBA bytes, row 0 first). The
compared image is its top-left 320 k x 240 k pixels (the back-buffer must be at least that large; the PS1 origin is the top-left
pixel; the G3c harness uses exactly 320 k x 240 k). Scenes: aura_t0, aura_t2, aura_t16, rays_163, pieces_391, mirror_476, mirror_327,
abr_378 (see g3c_expected.json). The scene may be omitted when the dump file name contains exactly one scene name.

Rule (plan, G3c acceptance): outside the noise zone every pixel must equal the prediction B(k) (top-left tie rule): opaque and
background pixels EXACT, pixels last written by a blended STP draw of a PSX mode (Mode0 to Mode2) within +-1 per channel (rule of
g2b1_compare.py and g2e_compare.py). Exact ties are kept and compared. The probes of g3c_expected.json are checked as well. The
prediction is recomputed with g3c_model.py and must equal the digest stored in g3c_expected.json (else exit code 2: the prediction
drifted). Exit code 0 when no pixel differs, 1 when one does, 2 on a usage or consistency error. Any other difference is a stop, to be
settled by proof, never by a new tolerance. The SHA-1 of the capture is NEVER compared (only the prediction's own digest is).
"""
import json
import os
import struct
import sys

import numpy as np

import g3c_model as model

HERE = os.path.dirname(os.path.abspath(__file__))


def load_dump(path):
    with open(path, 'rb') as f:
        width, height, k = struct.unpack('<iii', f.read(12))
        data = f.read(width * height * 4)
    if len(data) != width * height * 4:
        raise ValueError('truncated dump')
    return width, height, k, np.frombuffer(data, dtype=np.uint8).reshape(height, width, 4)


def compare(rgba, k, res, expected):
    """rgba: the dump pixels (h, w, 4), k the dump factor, res the prediction (model.render_scene), expected the per_k entry of
    g3c_expected.json. Returns (lines, differences, bad mask)."""
    W, H = model.PS1_W * k, model.PS1_H * k
    if rgba.shape[1] < W or rgba.shape[0] < H:
        return ['back-buffer %dx%d is smaller than the frame %dx%d' % (rgba.shape[1], rgba.shape[0], W, H)], 1, None
    lines = []
    read = rgba[:H, :W, :3].astype(np.int64)
    exp = res['img']
    diff = np.abs(read - exp).max(axis=2)
    keep = ~res['noise']
    tol = res['tol'].astype(np.int64)
    bad = (diff > tol) & keep
    opaque = keep & (tol == 0)
    blended = keep & (tol == 1)
    ties = keep & res['tie_any']
    lines.append('compared=%d noise_skipped=%d opaque_or_background=%d (differ %d) blended_pm1=%d (differ %d, max |diff| seen %d) exact_ties_kept=%d (differ %d)'
                 % (int(keep.sum()), int(res['noise'].sum()), int(opaque.sum()), int((bad & (tol == 0)).sum()), int(blended.sum()),
                    int((bad & (tol == 1)).sum()), int(diff[blended].max()) if blended.any() else 0, int(ties.sum()), int((bad & ties).sum())))
    for j, i in np.argwhere(bad)[:8]:
        lines.append('    example (%d, %d) read=%s expected=%s tol=%d' % (i, j, list(read[j, i]), list(exp[j, i]), tol[j, i]))
    probes_bad = 0
    for pr in expected['probes']:
        x, y = pr['x'], pr['y']
        r = read[y, x]
        if int(np.abs(r - np.array(pr['color'])).max()) > pr['tol']:
            probes_bad += 1
            lines.append('    probe (%s) (%d, %d) read=%s expected=%s tol=%d' % (pr['kind'], x, y, list(r), pr['color'], pr['tol']))
    lines.append('probes=%d differ=%d' % (len(expected['probes']), probes_bad))
    return lines, int(bad.sum()) + probes_bad, bad


def find_scene(args, path):
    if '--scene' in args:
        return args[args.index('--scene') + 1]
    hit = [n for n in model.SCENE_NAMES if n in os.path.basename(path)]
    return hit[0] if len(hit) == 1 else None


def load_expected():
    with open(os.path.join(HERE, 'g3c_expected.json'), encoding='utf-8') as f:
        return json.load(f)


def scene_of(expected, name):
    return model.scene_by_name(name, expected['abr_choice']['chosen'])


def run(path, scene_name, report=None, diff_png=None):
    width, height, k, rgba = load_dump(path)
    expected = load_expected()
    entry = [e for e in expected['scenes'] if e['name'] == scene_name]
    if not entry or str(k) not in entry[0]['per_k']:
        print('unknown scene %r or factor %d' % (scene_name, k))
        return 2
    exp_k = entry[0]['per_k'][str(k)]
    res = model.render_scene(scene_of(expected, scene_name), k)
    if model.digest(res['img']) != exp_k['sha1']:
        print('the recomputed prediction (%s) differs from g3c_expected.json (%s): regenerate with g3c_model.py' % (model.digest(res['img']), exp_k['sha1']))
        return 2
    lines = ['dump=%s backbuffer=%dx%d k=%d scene=%s frame=%dx%d prediction_sha1=%s' % (os.path.basename(path), width, height, k, scene_name, 320 * k, 240 * k, exp_k['sha1'])]
    more, bad, mask = compare(rgba, k, res, exp_k)
    lines += more
    lines.append('result=%s differences=%d' % ('PASS' if bad == 0 else 'FAIL', bad))
    text = '\n'.join(lines) + '\n'
    if report:
        with open(report, 'w', newline='\n') as f:
            f.write(text)
    if diff_png and mask is not None:
        from PIL import Image
        Image.fromarray((mask * 255).astype(np.uint8), 'L').save(diff_png)
    print(text)
    return 0 if bad == 0 else 1


def selftest():
    """The comparator on dumps built from the prediction: 0 on the exact image, 0 with +-1 on a blended pixel, 1 with +-2 on a blended
    pixel, 1 with +-1 on an opaque pixel, 0 when a noise pixel is wrong."""
    expected = load_expected()
    ok = True
    for name in model.SCENE_NAMES:
        for k in model.FACTORS:
            res = model.render_scene(scene_of(expected, name), k)
            entry = [e for e in expected['scenes'] if e['name'] == name][0]['per_k'][str(k)]
            W, H = 320 * k, 240 * k
            base = np.zeros((H + 5, W + 7, 4), dtype=np.uint8)       # a back-buffer larger than the frame
            base[:H, :W, :3] = res['img'].astype(np.uint8)
            base[..., 3] = 255

            def verdict(img):
                return compare(img, k, res, entry)[1]

            results = [('exact', verdict(base), 0)]
            bl = np.argwhere((res['tol'] == 1) & ~res['noise'])
            op = np.argwhere((res['tol'] == 0) & ~res['noise'] & (res['img'] != np.array(model.BG)).any(axis=2))
            nz = np.argwhere(res['noise'])
            if len(bl):
                j, i = bl[len(bl) // 2]
                m = base.copy()
                m[j, i, 1] = np.uint8(int(m[j, i, 1]) + 1) if m[j, i, 1] < 255 else np.uint8(254)
                results.append(('blended +-1', verdict(m), 0))
                m = base.copy()
                m[j, i, 1] = np.uint8(int(m[j, i, 1]) + 2) if m[j, i, 1] < 253 else np.uint8(int(m[j, i, 1]) - 2)
                results.append(('blended +-2', verdict(m), 1))
            if len(op):
                j, i = op[len(op) // 2]
                m = base.copy()
                m[j, i, 0] = np.uint8(int(m[j, i, 0]) + 1) if m[j, i, 0] < 255 else np.uint8(254)
                results.append(('opaque +-1', verdict(m), 1))
            if len(nz):
                j, i = nz[0]
                m = base.copy()
                m[j, i, :3] = (1, 2, 3)
                results.append(('noise pixel wrong', verdict(m), 0))
            for label, got, want in results:
                good = (got == 0) == (want == 0)
                ok &= good
                print('%-12s k=%d %-18s differences=%d %s' % (name, k, label, got, 'ok' if good else 'UNEXPECTED'))
    print('selftest', 'PASS' if ok else 'FAIL')
    return 0 if ok else 1


def main():
    np.seterr(invalid='ignore')
    args = sys.argv[1:]
    if '--selftest' in args:
        sys.exit(selftest())
    if not args or args[0].startswith('--'):
        print(__doc__)
        sys.exit(2)
    path = args[0]
    scene = find_scene(args, path)
    if scene is None:
        print('give --scene <name>')
        sys.exit(2)
    report = args[args.index('--report') + 1] if '--report' in args else None
    dpng = args[args.index('--diff-png') + 1] if '--diff-png' in args else None
    sys.exit(run(path, scene, report, dpng))


if __name__ == '__main__':
    main()
