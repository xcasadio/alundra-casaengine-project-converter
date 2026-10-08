# Own check of the six steady-state passes (long7/), periods and loudness windows.
import csv, os, sys
import numpy as np

SPF = 735
S = r"C:\Users\casad\AppData\Local\Temp\claude\D--development-repo-alundra-casaengine-project-converter\b00d1a72-420d-4acc-ab34-0c25fbdad3a1\scratchpad"
L7 = os.path.join(S, 'loop-data', 'long7')


def load(idx):
    p = os.path.join(L7, 'bgm_%03d.wav' % idx)
    n = (os.path.getsize(p) - 44) // 4
    return np.memmap(p, dtype='<i2', mode='r', offset=44, shape=(n, 2))


def win_db(x, start_frame, nframes, wf):
    m = nframes // wf
    seg = np.asarray(x[start_frame * SPF:(start_frame + m * wf) * SPF], dtype=np.float64).reshape(m, wf * SPF, 2)
    rms = np.sqrt((seg ** 2).mean(axis=1))
    return 20 * np.log10(np.maximum(rms, 1.0) / 32768.0)


def worst(a, b, thr):
    n = min(len(a), len(b))
    a, b = a[:n], b[:n]
    m = np.maximum(a.max(axis=1), b.max(axis=1)) > thr
    if not m.any():
        return 0.0, -1
    d = np.abs(a - b).max(axis=1)
    d[~m] = 0
    i = int(d.argmax())
    return float(d[i]), i


rows = list(csv.DictReader(open(os.path.join(L7, 'events.tsv')), delimiter='\t'))
vary = []; const = []
res = {}
for r in rows:
    if not r['Jumps']:
        continue
    idx = int(r['SoundIndex']); J = [int(v) for v in r['Jumps'].split(',')]
    per = [J[k + 1] - J[k] for k in range(len(J) - 1)]
    (const if len(set(per)) == 1 else vary).append(idx)
    x = load(idx)
    out = {}
    for name, wf, thr in (('w30_-50', 30, -50.0), ('w60_-40', 60, -40.0)):
        envs = [win_db(x, J[k], J[k + 1] - J[k], wf) for k in range(6)]  # passes 2..7
        v2 = [worst(envs[k], envs[0], thr) for k in range(1, 6)]
        out[name] = (max(v[0] for v in v2), [round(v[0], 1) for v in v2])
        # pairwise worst over all passes 2..7 (any pass as reference)
        pw = max(worst(envs[i], envs[j], thr)[0] for i in range(6) for j in range(i + 1, 6))
        out[name + '_pairwise'] = round(pw, 1)
    res[idx] = out
    print(idx, 'periods', per, out, flush=True)
print('periods vary on', len(vary), vary)
print('periods constant on', len(const), const)
for name in ('w30_-50', 'w60_-40'):
    within = [i for i in res if res[i][name][0] <= 3.2]
    print(name, 'within 3.2 dB of pass 2:', len(within), 'exceptions', sorted(((i, round(res[i][name][0], 1)) for i in res if res[i][name][0] > 3.2), key=lambda t: -t[1]))
# track 10 detail: windows around +2400 and +2880 frames, passes 1..7 (pass 1 = [LS,J1) aligned to LS)
x = load(10)
J = [int(v) for v in [r for r in rows if r['SoundIndex'] == '10'][0]['Jumps'].split(',')]
LS = 63
starts = [LS] + J[:6]
for off in (2400, 2880):
    vals = []
    for s0 in starts:
        seg = np.asarray(x[(s0 + off) * SPF:(s0 + off + 30) * SPF], dtype=np.float64)
        vals.append(round(20 * np.log10(max(np.sqrt((seg[:, 0] ** 2).mean()), 1) / 32768), 1))
    print('track 10 window +%d (left dB) passes 1..7:' % off, vals)
