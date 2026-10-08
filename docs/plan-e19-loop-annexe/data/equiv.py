# Is iteration 3 (what the original plays after J2) the same music as iteration 2 (what the B file loops back to)?
# Sample-exact comparison fails on 35 of 38 tracks because the original's own frame quantisation moves events by +-1
# frame between iterations. This compares per-frame loudness envelopes (735-sample RMS, per channel, in dB) with a
# +-1 frame tolerance, for iteration 3 vs 2 (jitter only, the steady state) and iteration 2 vs 1 (the first pass,
# D-E19-72), plus the seam counts. Writes equiv.tsv and prints a summary.
import csv, os
import numpy as np

SPF = 735
HERE = os.path.dirname(os.path.abspath(__file__))
LOOPS = os.path.join(HERE, '..', 'loop-disc', 'loops.tsv')


def load(idx):
    p = os.path.join(HERE, 'long', 'bgm_%03d.wav' % idx)
    n = (os.path.getsize(p) - 44) // 4
    return np.memmap(p, dtype='<i2', mode='r', offset=44, shape=(n, 2)).astype(np.float64)


def env_db(x, f0, n):
    seg = x[f0 * SPF:(f0 + n) * SPF].reshape(n, SPF, 2)
    rms = np.sqrt((seg ** 2).mean(axis=1))
    return 20 * np.log10(np.maximum(rms, 1.0) / 32768.0)


def tolerant(a, b, floor_db=-50.0):
    n = min(len(a), len(b))
    a, b = a[:n], b[:n]
    best = np.full(n, np.inf)
    for s in (-1, 0, 1):
        lo, hi = max(0, -s), min(n, n - s)
        d = np.abs(a[lo:hi] - b[lo + s:hi + s]).max(axis=1)
        best[lo:hi] = np.minimum(best[lo:hi], d)
    loud = np.maximum(a.max(axis=1), b.max(axis=1)) > floor_db
    d = best[loud]
    return d, int(loud.sum())


def stats(d):
    if len(d) == 0:
        return dict(p50=0, p95=0, p99=0, max=0, gt3=0)
    return dict(p50=round(float(np.percentile(d, 50)), 2), p95=round(float(np.percentile(d, 95)), 2),
                p99=round(float(np.percentile(d, 99)), 2), max=round(float(d.max()), 2), gt3=round(float((d > 3).mean()), 4))


def main():
    ev = {}
    with open(os.path.join(HERE, 'long', 'events.tsv'), newline='') as f:
        for r in csv.DictReader(f, delimiter='\t'):
            if r['Jumps']:
                ev[int(r['SoundIndex'])] = [int(v) for v in r['Jumps'].split(',')]
    lt = {}
    with open(LOOPS, newline='') as f:
        for r in csv.DictReader(f, delimiter='\t'):
            lt[int(r['idx'])] = r
    seam = {}
    with open(os.path.join(HERE, 'seam_b.tsv'), newline='') as f:
        for r in csv.DictReader(f, delimiter='\t'):
            seam[int(r['idx'])] = r
    out = []
    for idx in sorted(ev):
        j1, j2, j3 = ev[idx][:3]
        ls = int(lt[idx]['LS_frame'])
        x = load(idx)
        n32 = min(j2 - j1, j3 - j2)
        e2 = env_db(x, j1, n32)
        e3 = env_db(x, j2, n32)
        d32, c32 = tolerant(e3, e2)
        n21 = min(j1 - ls, j2 - j1)
        e1 = env_db(x, ls, n21)
        e2b = env_db(x, j1, n21)
        d21, c21 = tolerant(e2b, e1)
        s32, s21 = stats(d32), stats(d21)
        sm = seam[idx]
        row = {'idx': idx, 'iter1_differs_report': lt[idx]['iter1_differs_from_steady_state'],
               'i3v2_frames': c32, **{'i3v2_' + k: v for k, v in s32.items()},
               'i2v1_frames': c21, **{'i2v1_' + k: v for k, v in s21.items()},
               'step_p99': sm['step_p99'], 'step_max': sm['step_max'], 'step_true_at_J2': sm['step_true_at_J2'],
               'step_seam_noxf': sm['step_seam_noxf'], 'step_seam_xf735': sm['step_seam_xf735']}
        out.append(row)
    with open(os.path.join(HERE, 'equiv.tsv'), 'w', newline='') as f:
        w = csv.DictWriter(f, fieldnames=list(out[0].keys()), delimiter='\t', lineterminator='\n')
        w.writeheader(); w.writerows(out)

    noxf = [(r['idx'], float(r['step_seam_noxf']), float(r['step_p99']), float(r['step_max']), float(r['step_true_at_J2'])) for r in out]
    print('seam step no crossfade > p99 natural step:', sum(1 for i, s, p, m, t in noxf if s > p), [i for i, s, p, m, t in noxf if s > p])
    print('seam step no crossfade > 2 x p99:', sum(1 for i, s, p, m, t in noxf if s > 2 * p), [i for i, s, p, m, t in noxf if s > 2 * p])
    print('seam step no crossfade > max natural step:', [i for i, s, p, m, t in noxf if s > m])
    print('true step at J2 > p99:', sum(1 for i, s, p, m, t in noxf if t > p), [i for i, s, p, m, t in noxf if t > p])
    xf = [(r['idx'], float(r['step_seam_xf735']), float(r['step_p99'])) for r in out]
    print('seam step with crossfade > p99:', sum(1 for i, s, p in xf if s > p), [i for i, s, p in xf if s > p])
    print('noxf/p99 median %.2f max %.2f' % (np.median([s / p for i, s, p, m, t in noxf]), max(s / p for i, s, p, m, t in noxf)))
    print('xf/p99 median %.2f max %.2f' % (np.median([s / p for i, s, p in xf]), max(s / p for i, s, p in xf)))
    print('envelope i3 vs i2 (+-1 frame): worst p95 %.2f dB, worst p99 %.2f dB, tracks with p95 > 1 dB: %s' % (
        max(r['i3v2_p95'] for r in out), max(r['i3v2_p99'] for r in out), [r['idx'] for r in out if r['i3v2_p95'] > 1]))
    print('envelope i2 vs i1 (+-1 frame): tracks with p95 > 1 dB: %s' % [(r['idx'], r['i2v1_p95']) for r in out if r['i2v1_p95'] > 1])
    print('frac frames > 3 dB i3v2 max %.4f ; i2v1 per track > 0.01: %s' % (max(r['i3v2_gt3'] for r in out), [(r['idx'], r['i2v1_gt3']) for r in out if r['i2v1_gt3'] > 0.01]))


if __name__ == '__main__':
    main()
