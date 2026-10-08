# Seam and tail measures for option B (D-E19-73), from the long renders of the harness (long/bgm_NNN.wav, events.tsv).
# Run from the loop-data folder. Writes seam_b.tsv, tails.tsv and prints a summary.
import csv, os, sys
import numpy as np

SPF = 735
HERE = os.path.dirname(os.path.abspath(__file__))
LONG = os.path.join(HERE, 'long')
LOOPS = os.path.join(HERE, '..', 'loop-disc', 'loops.tsv')


def load(idx):
    p = os.path.join(LONG, 'bgm_%03d.wav' % idx)
    with open(p, 'rb') as f:
        hdr = f.read(44)
    assert hdr[:4] == b'RIFF' and hdr[8:12] == b'WAVE'
    n = int.from_bytes(hdr[40:44], 'little') // 4
    return np.memmap(p, dtype='<i2', mode='r', offset=44, shape=(n, 2)).astype(np.int32)


def events():
    rows = {}
    with open(os.path.join(LONG, 'events.tsv'), newline='') as f:
        for r in csv.DictReader(f, delimiter='\t'):
            idx = int(r['SoundIndex'])
            r['jumps'] = [int(x) for x in r['Jumps'].split(',')] if r['Jumps'] else []
            r['ends'] = [int(x) for x in r['Ends'].split(',')] if r['Ends'] else []
            rows[idx] = r
    return rows


def loops_tsv():
    rows = {}
    with open(LOOPS, newline='') as f:
        for r in csv.DictReader(f, delimiter='\t'):
            rows[int(r['idx'])] = r
    return rows


def crossfade(x, j1, j2, k):
    """Integer crossfade, same formula as the harness: out[a0+x] = round((s2*(k-(x+1)) + s1*(x+1)) / k)."""
    y = x.copy()
    a0 = j2 * SPF - k
    b0 = j1 * SPF - k
    w = np.arange(1, k + 1, dtype=np.int64)[:, None]
    s2 = x[a0:a0 + k].astype(np.int64)
    s1 = x[b0:b0 + k].astype(np.int64)
    num = s2 * (k - w) + s1 * w
    half = k // 2
    q = np.where(num >= 0, (num + half) // k, -((-num + half) // k))
    y[a0:a0 + k] = q.astype(np.int32)
    return y


def first_exceed(e, thr):
    m = np.abs(e).max(axis=1) > thr
    idx = np.argmax(m)
    return int(idx) if m.any() else -1


def trailing_within(e, thr):
    """Number of trailing samples (counting back from the last one) with |e| <= thr on both channels."""
    m = np.abs(e).max(axis=1) > thr
    if not m.any():
        return len(e)
    last_bad = len(m) - 1 - int(np.argmax(m[::-1]))
    return len(m) - 1 - last_bad


def rms(a):
    return float(np.sqrt(np.mean(a.astype(np.float64) ** 2))) if len(a) else 0.0


def max_silent_run(x, nframes):
    fr = np.abs(x[:nframes * SPF]).max(axis=1).reshape(nframes, SPF).max(axis=1)
    aud = fr > 64
    best = run = 0
    for a in aud:
        run = 0 if a else run + 1
        best = max(best, run)
    return best, fr


def main():
    ev = events()
    lt = loops_tsv()
    seam_rows = []
    tail_rows = []
    for idx in sorted(ev):
        r = ev[idx]
        x = load(idx)
        if r['jumps']:
            j1, j2, j3 = r['jumps'][:3]
            a, b = j1 * SPF, j2 * SPF
            fileN = j2  # frames kept by option B
            steps = np.abs(np.diff(x[:b], axis=0)).max(axis=1)
            p99 = float(np.percentile(steps, 99))
            p999 = float(np.percentile(steps, 99.9))
            smax = int(steps.max())
            step_true_j2 = int(np.abs(x[b] - x[b - 1]).max())
            step_true_j1 = int(np.abs(x[a] - x[a - 1]).max())
            step_naive = int(np.abs(x[a] - x[b - 1]).max())
            res = {'idx': idx, 'J1': j1, 'J2': j2, 'J3': j3, 'P2': j2 - j1, 'P3': j3 - j2,
                   'loops_tsv_J1': lt[idx]['J1_frame'], 'loops_tsv_J2': lt[idx]['J2_frame'], 'loops_tsv_P3': lt[idx]['P3_frames'],
                   'step_p99': round(p99, 1), 'step_p999': round(p999, 1), 'step_max': smax,
                   'step_true_at_J2': step_true_j2, 'step_true_at_J1': step_true_j1, 'step_seam_noxf': step_naive}
            for k in (735,):
                y = crossfade(x[:b], j1, j2, k)
                res['step_seam_xf%d' % k] = int(np.abs(x[a] - y[b - 1]).max())
                region = y[b - k - 1:b]
                res['xf%d_max_step_in_region' % k] = int(np.abs(np.diff(region, axis=0)).max())
                res['orig_max_step_in_region'] = int(np.abs(np.diff(x[b - k - 1:b], axis=0)).max())
                d = (y[b - k:b] - x[b - k:b])
                res['xf%d_change_max' % k] = int(np.abs(d).max())
                res['xf%d_change_rms' % k] = round(rms(d), 1)
            # pre-seam: end of iteration 2 against end of iteration 1
            n_pre = min(j2 - j1, j1) * SPF
            e_pre = x[b - n_pre:b] - x[a - n_pre:a]
            res['pre_equal_trailing_samples'] = trailing_within(e_pre, 0)
            res['pre_within64_trailing_samples'] = trailing_within(e_pre, 64)
            res['pre_lastframe_err_max'] = int(np.abs(e_pre[-SPF:]).max())
            res['pre_lastframe_err_rms'] = round(rms(e_pre[-SPF:]), 1)
            res['lastframe_signal_rms'] = round(rms(x[b - SPF:b]), 1)
            # post-seam: true continuation (iteration 3 from J2) against what the loop plays (iteration 2 from J1)
            n_post = min(j3 - j2, j2 - j1) * SPF
            e_post = x[b:b + n_post] - x[a:a + n_post]
            for thr in (0, 16, 64, 256):
                res['post_first_gt%d' % thr] = first_exceed(e_post, thr)
            res['post_err_rms_frame0'] = round(rms(e_post[:SPF]), 1)
            res['post_err_rms_60f'] = round(rms(e_post[:60 * SPF]), 1)
            res['post_signal_rms_60f'] = round(rms(x[b:b + 60 * SPF]), 1)
            res['post_err_rms_iter'] = round(rms(e_post), 1)
            res['post_signal_rms_iter'] = round(rms(x[b:b + n_post]), 1)
            fe = np.abs(e_post[:(n_post // SPF) * SPF]).max(axis=1).reshape(-1, SPF).max(axis=1)
            res['post_frames_err_gt64'] = int((fe > 64).sum())
            res['post_frames_compared'] = int(len(fe))
            # frame J3 against frames J1 and J2 (the jump frame of each iteration)
            res['frameJ3_eq_frameJ2'] = bool(np.array_equal(x[j3 * SPF:(j3 + 1) * SPF], x[b:b + SPF]))
            res['frameJ2_eq_frameJ1'] = bool(np.array_equal(x[b:b + SPF], x[a:a + SPF]))
            ms, _ = max_silent_run(x, j2)
            res['max_silent_run_0_J2'] = ms
            seam_rows.append(res)
        else:
            e = r['ends'][0]
            n = len(x) // SPF
            fr = np.abs(x[:n * SPF]).max(axis=1).reshape(n, SPF).max(axis=1)
            after = fr[e + 1:]
            aud = np.nonzero(after > 64)[0]
            nz = np.nonzero(after > 0)[0]
            first_sil = int(np.argmax(after <= 64)) if (after <= 64).any() else -1
            row = {'idx': idx, 'End': e, 'today_Frames': e + 1,
                   'endframe_peak': int(fr[e]), 'endframe_rms': round(rms(x[e * SPF:(e + 1) * SPF]), 1),
                   'first_silent_frame_after_end_rel': first_sil,
                   'last_audible_frame_after_end_rel': int(aud[-1]) if len(aud) else -1,
                   'audible_frames_after_end': int(len(aud)),
                   'last_nonzero_frame_after_end_rel': int(nz[-1]) if len(nz) else -1,
                   'rendered_after_end': int(len(after))}
            ms, _ = max_silent_run(x, e + 1)
            row['max_silent_run_0_end'] = ms
            tail_rows.append(row)

    with open(os.path.join(HERE, 'seam_b.tsv'), 'w', newline='') as f:
        w = csv.DictWriter(f, fieldnames=list(seam_rows[0].keys()), delimiter='\t')
        w.writeheader()
        w.writerows(seam_rows)
    with open(os.path.join(HERE, 'tails.tsv'), 'w', newline='') as f:
        w = csv.DictWriter(f, fieldnames=list(tail_rows[0].keys()), delimiter='\t')
        w.writeheader()
        w.writerows(tail_rows)
    print('looping', len(seam_rows), 'one-shot', len(tail_rows))


if __name__ == '__main__':
    main()
