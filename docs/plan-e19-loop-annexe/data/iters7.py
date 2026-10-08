# Steady state over six iterations (long7/: renders through seven loop jumps J1..J7).
# Iteration k (k>=2) is [J(k-1), J(k)); iteration 2 is what the option-B file loops.
# Per track: period of each iteration, sample-exact equality of iterations (aligned at their start, over the shorter),
# and the worst 500 ms loudness-window difference (dB, per channel, windows louder than -50 dBFS) of every iteration
# against iteration 2 and against its predecessor. Writes iters7.tsv.
import csv, os, sys
import numpy as np

SPF = 735
W = 30
HERE = os.path.dirname(os.path.abspath(__file__))


def load(idx):
    p = os.path.join(HERE, 'long7', 'bgm_%03d.wav' % idx)
    n = (os.path.getsize(p) - 44) // 4
    return np.memmap(p, dtype='<i2', mode='r', offset=44, shape=(n, 2))


def env(x, f0, n):
    m = n // W
    seg = x[f0 * SPF:(f0 + m * W) * SPF].astype(np.float64).reshape(m, W * SPF, 2)
    return 20 * np.log10(np.maximum(np.sqrt((seg ** 2).mean(axis=1)), 1.0) / 32768.0)


def envdiff(a, b):
    n = min(len(a), len(b))
    a, b = a[:n], b[:n]
    loud = np.maximum(a.max(axis=1), b.max(axis=1)) > -50
    d = np.abs(a - b).max(axis=1)[loud]
    return (float(d.max()) if len(d) else 0.0), (float(np.percentile(d, 95)) if len(d) else 0.0), (int((d > 3).sum()))


def main():
    rows = []
    with open(os.path.join(HERE, 'long7', 'events.tsv'), newline='') as f:
        for r in csv.DictReader(f, delimiter='\t'):
            if not r['Jumps']:
                continue
            idx = int(r['SoundIndex'])
            J = [int(v) for v in r['Jumps'].split(',')]
            if len(J) < 7:
                rows.append({'idx': idx, 'note': 'only %d jumps' % len(J)})
                continue
            x = load(idx)
            periods = [J[k + 1] - J[k] for k in range(6)]  # iterations 2..7
            its = [(J[k], J[k + 1] - J[k]) for k in range(6)]
            exact_vs_prev = []
            for k in range(1, 6):
                (a0, na), (b0, nb) = its[k - 1], its[k]
                n = min(na, nb) * SPF
                exact_vs_prev.append(bool(np.array_equal(x[a0 * SPF:a0 * SPF + n], x[b0 * SPF:b0 * SPF + n])))
            envs = [env(x, s, n) for s, n in its]
            vs2 = [envdiff(envs[k], envs[0]) for k in range(1, 6)]
            vsprev = [envdiff(envs[k], envs[k - 1]) for k in range(1, 6)]
            vs_k2 = [envdiff(envs[k], envs[k - 2]) for k in range(2, 6)]
            rows.append({'idx': idx, 'J': ','.join(map(str, J)), 'periods_it2_to_it7': ','.join(map(str, periods)),
                         'exact_it3..7_vs_prev': ''.join('1' if e else '0' for e in exact_vs_prev),
                         'max_dB_it3..7_vs_it2': ','.join('%.1f' % v[0] for v in vs2),
                         'max_dB_it3..7_vs_prev': ','.join('%.1f' % v[0] for v in vsprev),
                         'max_dB_it4..7_vs_two_before': ','.join('%.1f' % v[0] for v in vs_k2),
                         'windows_gt3dB_it3..7_vs_it2': ','.join(str(v[2]) for v in vs2),
                         'worst_vs_it2': round(max(v[0] for v in vs2), 1)})
    keys = []
    for r in rows:
        for k in r:
            if k not in keys:
                keys.append(k)
    with open(os.path.join(HERE, 'iters7.tsv'), 'w', newline='') as f:
        w = csv.DictWriter(f, fieldnames=keys, delimiter='\t', lineterminator='\n', restval='')
        w.writeheader(); w.writerows(rows)
    for r in rows:
        print('\t'.join(str(r.get(k, '')) for k in keys))


if __name__ == '__main__':
    main()
