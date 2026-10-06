"""E19.g G2b-1: compare the back-buffer dump of the engine demo "PSX free quads" with the prediction g2b1_predictions.json.

Usage:  python g2b1_compare.py <dump.bin> [--report out.txt]
The dump is written by the demo (int32 width, int32 height, int32 k, then width * height RGBA bytes). For every case of the
prediction at that factor: each covered pixel outside the noise zone must read the colour of B(k) (exact for opaque pixels, +-1
per channel for the blended pixels of the PSX modes), each uncovered pixel outside the noise zone must read the background; each
probe must read its colour; the three 1:1 rows (a) quad and (b) rectangle path must read the same pixels. Exit code 0 when
there is no difference, 1 otherwise.
"""
import json
import os
import struct
import sys

import g2b1_predict as model

HERE = os.path.dirname(os.path.abspath(__file__))


def load_dump(path):
    with open(path, 'rb') as f:
        width, height, k = struct.unpack('<iii', f.read(12))
        data = f.read(width * height * 4)
    assert len(data) == width * height * 4
    return width, height, k, data


def pixel(dump, x, y):
    width, height, _, data = dump
    if not (0 <= x < width and 0 <= y < height):
        return None
    o = (y * width + x) * 4
    return data[o], data[o + 1], data[o + 2]


def close(read, exp):
    return all(abs(read[i] - exp[i]) <= exp[3] for i in range(3))


def main():
    path = sys.argv[1]
    dump = load_dump(path)
    k = dump[2]
    with open(os.path.join(HERE, 'g2b1_predictions.json'), encoding='utf-8') as f:
        pred = json.load(f)
    cases = {c['name']: c for c in model.CASES}
    lines = [f'dump={os.path.basename(path)} backbuffer={dump[0]}x{dump[1]} k={k}']
    total_bad = 0
    for entry in pred['cases']:
        case = cases[entry['name']]
        info = entry['per_k'][str(k)]
        x0, y0, x1, y1 = info['box']
        w = info['w']
        covered = bad_covered = uncovered = bad_uncovered = skipped = 0
        examples = []
        for sy in range(y0, y1):
            for sx in range(x0, x1):
                idx = (sy - y0) * w + (sx - x0)
                if info['noise'][idx]:
                    skipped += 1
                    continue
                code = info['texel_codes'][idx]
                texel = None if code < 0 else (code // model.TEX, code % model.TEX)
                exp = model.expected_color(case, texel)
                read = pixel(dump, sx, sy)
                ok = read is not None and close(read, exp)
                if texel is None:
                    uncovered += 1
                    bad_uncovered += 0 if ok else 1
                else:
                    covered += 1
                    bad_covered += 0 if ok else 1
                if not ok and len(examples) < 5:
                    examples.append((sx, sy, texel, read, exp))
        probes_bad = 0
        for pr in info['probes']:
            read = pixel(dump, pr['x'], pr['y'])
            exp = tuple(pr['color'])
            if read is None or not close(read, exp):
                probes_bad += 1
                examples.append(('probe', pr['x'], pr['y'], read, exp))
        total_bad += bad_covered + bad_uncovered + probes_bad
        lines.append(f"{entry['name']:15s} covered_kept={covered} (expected {info['denominator']}) differ={bad_covered} "
                     f"uncovered_kept={uncovered} differ={bad_uncovered} noise_skipped={skipped} probes={len(info['probes'])} probes_differ={probes_bad}"
                     + ('' if covered == info['denominator'] else ' DENOMINATOR-MISMATCH'))
        if covered != info['denominator']:
            total_bad += 1
        for e in examples:
            lines.append(f'    example {e}')
    # 1:1 rows: (a) and (b) read the same pixels, pixel for pixel
    for a, b in model.ROW_PAIRS:
        ia = [e for e in pred['cases'] if e['name'] == a][0]['per_k'][str(k)]
        ib = [e for e in pred['cases'] if e['name'] == b][0]['per_k'][str(k)]
        diff = 0
        count = 0
        for j in range(ia['h']):
            for i in range(ia['w']):
                pa = pixel(dump, ia['box'][0] + i, ia['box'][1] + j)
                pb = pixel(dump, ib['box'][0] + i, ib['box'][1] + j)
                count += 1
                if pa != pb:
                    diff += 1
        lines.append(f'row {a} vs {b}: pixels={count} differ={diff}')
        total_bad += diff
    lines.append(f'result={"PASS" if total_bad == 0 else "FAIL"} differences={total_bad}')
    text = '\n'.join(lines) + '\n'
    if '--report' in sys.argv:
        with open(sys.argv[sys.argv.index('--report') + 1], 'w', newline='\n') as f:
            f.write(text)
    print(text)
    sys.exit(0 if total_bad == 0 else 1)


if __name__ == '__main__':
    main()
