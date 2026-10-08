# Independent recomputation of the L-b oracle values (read-only).
# Sources (all read-only):
#   DE   = repo data-extracted/sound (today, X2)
#   ORA  = loop-data/oracle/sound (claimed oracle)
#   CLI  = loop-data/renderbgm/bgm_NNN_csharp_render.wav (unchanged extractor --render-bgm --frames N)
#   LONG = loop-data/long/bgm_NNN.wav (harness renders past J3 / E+1800)
#   SEQ  = loop-disc-verify/seqsim.out (independent Python sequencer, 'ana' variant)
import hashlib, json, math, os, re, struct, sys
import numpy as np

SPF = 735
S = r"C:\Users\casad\AppData\Local\Temp\claude\D--development-repo-alundra-casaengine-project-converter\b00d1a72-420d-4acc-ab34-0c25fbdad3a1\scratchpad"
LD = os.path.join(S, 'loop-data')
DE = r"D:\development\repo\alundra-casaengine-project-converter\data-extracted\sound"
ORA = os.path.join(LD, 'oracle', 'sound')
OUT = os.path.join(S, 'loop-data-verify')


def sha(p):
    h = hashlib.sha256()
    with open(p, 'rb') as f:
        for c in iter(lambda: f.read(1 << 22), b''):
            h.update(c)
    return h.hexdigest()


def wav(p, mm=False):
    with open(p, 'rb') as f:
        hd = f.read(44)
    assert hd[0:4] == b'RIFF' and hd[8:12] == b'WAVE' and hd[12:16] == b'fmt ', p
    fmtlen, tag, ch, rate, brate, align, bits = struct.unpack('<ihhiihh', hd[16:36])
    assert (fmtlen, tag, ch, rate, brate, align, bits) == (16, 1, 2, 44100, 176400, 4, 16), (p, fmtlen, tag, ch, rate)
    assert hd[36:40] == b'data'
    n = struct.unpack('<i', hd[40:44])[0]
    riff = struct.unpack('<i', hd[4:8])[0]
    size = os.path.getsize(p)
    assert size == 44 + n and riff == 36 + n, (p, size, n, riff)
    if mm:
        return np.memmap(p, dtype='<i2', mode='r', offset=44, shape=(n // 4, 2))
    return np.fromfile(p, dtype='<i2', offset=44).reshape(-1, 2)


def my_xfade(x, j1, j2, k=735):
    y = np.array(x, dtype=np.int64)
    a0, b0 = j2 * SPF - k, j1 * SPF - k
    w = np.arange(1, k + 1, dtype=np.int64)[:, None]
    num = y[a0:a0 + k] * (k - w) + y[b0:b0 + k] * w
    # round to nearest (no ties: k odd), symmetric
    q = np.sign(num) * ((np.abs(num) * 2 + k) // (2 * k))
    y[a0:a0 + k] = q
    return y.astype(np.int16)


def stats(x):
    # mimic C#: long sums, (double)sum / (double)total
    xl = x[:, 0].astype(np.int64); xr = x[:, 1].astype(np.int64)
    ssl = int((xl * xl).sum()); ssr = int((xr * xr).sum())
    tot = float(len(x))
    rl = math.sqrt(float(ssl) / tot); rr = math.sqrt(float(ssr) / tot)
    pl = int(np.abs(xl).max()); pr = int(np.abs(xr).max())
    aud = (np.abs(xl) > 64) | (np.abs(xr) > 64)
    fa = int(np.argmax(aud)) // SPF if aud.any() else -1
    return pl, pr, rl, rr, fa


def frame_audible(x):
    nf = len(x) // SPF
    a = np.abs(x[:nf * SPF].astype(np.int32)).reshape(nf, SPF * 2).max(axis=1)
    return a > 64


def seqsim():
    r = {}
    for line in open(os.path.join(S, 'loop-disc-verify', 'seqsim.out')):
        m = re.match(r'^(\d+) .*\| ana J \[([^\]]*)\] end (\S+)', line)
        if m:
            idx = int(m.group(1))
            J = [int(v) for v in m.group(2).split(',')] if m.group(2).strip() else []
            end = None if m.group(3) == 'None' else int(m.group(3))
            r[idx] = (J, end)
    return r


def main():
    today = {e['SoundIndex']: e for e in json.load(open(os.path.join(DE, 'bgm.json'), encoding='utf-8'))}
    ora = {e['SoundIndex']: e for e in json.load(open(os.path.join(ORA, 'bgm.json'), encoding='utf-8'))}
    ss = seqsim()
    print('today bgm.json sha', sha(os.path.join(DE, 'bgm.json')), 'entries', len(today))
    print('oracle bgm.json sha', sha(os.path.join(ORA, 'bgm.json')), 'entries', len(ora), 'bytes', os.path.getsize(os.path.join(ORA, 'bgm.json')))
    rows = []
    tot_today = tot_ora = 0
    for idx in sorted(ora):
        e = ora[idx]; t = today[idx]
        fn = 'bgm_%03d.wav' % idx
        po = os.path.join(ORA, 'bgm', fn)
        pt = os.path.join(DE, 'bgm', fn)
        pc = os.path.join(LD, 'renderbgm', 'bgm_%03d_csharp_render.wav' % idx)
        pl = os.path.join(LD, 'long', fn)
        o = wav(po); td = wav(pt); c = wav(pc); lg = wav(pl, mm=True)
        so = os.path.getsize(po); st = os.path.getsize(pt)
        tot_today += st; tot_ora += so
        J, end = ss[idx]
        r = {'idx': idx, 'sha': sha(po), 'bytes': so, 'frames_json': e['Frames'], 'frames_wav': len(o) / SPF,
             'today_frames': t['Frames'], 'today_bytes': st,
             'seq_J': ','.join(map(str, J[:2])), 'seq_end': end,
             'looping': e.get('Looping'), 'lsf': e.get('LoopStartFrame'), 'lss': e.get('LoopStartSample')}
        # today prefix
        r['today_prefix'] = bool(np.array_equal(o[:len(td)], td))
        # CLI raw equals oracle except crossfade window
        r['cli_frames'] = len(c) / SPF
        long_prefix_eq = bool(np.array_equal(np.asarray(lg[:len(c)]), c))
        r['long_prefix_eq_cli'] = long_prefix_eq
        if e.get('Looping'):
            j1, j2 = J[0], J[1]
            r['J_ok'] = (e['LoopStartFrame'] == j1 and e['Frames'] == j2 and e['LoopStartSample'] == j1 * SPF)
            r['J1_eq_todayFrames-1'] = (t['Frames'] == j1 + 1)
            y = my_xfade(c, j1, j2)
            r['oracle_eq_cli_xf'] = bool(np.array_equal(y, o))
            diff = np.nonzero((c != o).any(axis=1))[0]
            r['xf_changed'] = len(diff)
            r['xf_range_ok'] = bool(len(diff) == 0 or (diff.min() >= j2 * SPF - SPF and diff.max() < j2 * SPF))
            r['xf_maxabs'] = int(np.abs(c.astype(np.int32) - o.astype(np.int32)).max())
            # seam values
            a = o.astype(np.int32); raw = c.astype(np.int32)
            st_all = np.abs(np.diff(a, axis=0)).max(axis=1)
            r['p99_xf'] = float(np.percentile(st_all, 99)); r['max_xf'] = int(st_all.max())
            st_raw = np.abs(np.diff(raw, axis=0)).max(axis=1)
            r['p99_raw'] = float(np.percentile(st_raw, 99)); r['max_raw'] = int(st_raw.max())
            r['seam_noxf'] = int(np.abs(raw[j1 * SPF] - raw[j2 * SPF - 1]).max())
            r['seam_xf'] = int(np.abs(a[j1 * SPF] - a[j2 * SPF - 1]).max())
            r['natural_J1'] = int(np.abs(raw[j1 * SPF] - raw[j1 * SPF - 1]).max())
            L = np.asarray(lg).astype(np.int32)
            r['true_J2'] = int(np.abs(L[j2 * SPF] - L[j2 * SPF - 1]).max())
            r['last_eq_beforeJ1_raw'] = bool(np.array_equal(raw[j2 * SPF - 1], raw[j1 * SPF - 1]))
            r['last_eq_beforeJ1_xf'] = bool(np.array_equal(a[j2 * SPF - 1], a[j1 * SPF - 1]))
            r['last_xf'] = tuple(int(v) for v in a[j2 * SPF - 1]); r['beforeJ1'] = tuple(int(v) for v in a[j1 * SPF - 1])
            r['last_raw'] = tuple(int(v) for v in raw[j2 * SPF - 1])
            # last frame of pass 2 vs pass 1
            f2 = raw[(j2 - 1) * SPF:j2 * SPF].astype(np.float64); f1 = raw[(j1 - 1) * SPF:j1 * SPF].astype(np.float64)
            r['lastframe_identical'] = bool(np.array_equal(f2, f1))
            r['lastframe_err_rms'] = float(np.sqrt(((f2 - f1) ** 2).mean()))
            r['lastframe_sig_rms'] = float(np.sqrt((f2 ** 2).mean()))
            # true continuation: L[J2*735+n] vs file[J1*735+n] (looped file = oracle) for one pass
            npass = (j2 - j1) * SPF
            avail = min(npass, len(L) - j2 * SPF)
            d = np.abs(L[j2 * SPF:j2 * SPF + avail] - a[j1 * SPF:j1 * SPF + avail]).max(axis=1)
            over = np.nonzero(d > 64)[0]
            r['cont_first_gt64'] = int(over[0]) if len(over) else 'never(%d)' % avail
            # silent runs before J2
            aud = frame_audible(raw[:j2 * SPF])
            run = best = 0
            for v in aud:
                run = 0 if v else run + 1
                best = max(best, run)
            r['max_silent_run'] = best
        else:
            # one-shot: oracle equals CLI raw (no crossfade)
            r['oracle_eq_cli'] = bool(np.array_equal(c, o))
            E = end
            r['E'] = E
            L = np.asarray(lg)
            aud = frame_audible(L)
            # tail rule: after E, run until 60 silent frames in a row (counter continuing across E), keep up to last audible
            run = 0; last = -1; stop = None
            for f in range(len(aud)):
                if aud[f]:
                    last = f
                run = 0 if aud[f] else run + 1
                if f > E and run >= 60:
                    stop = f; break
                if f == E:
                    pass
            r['tail_new_frames'] = max(E + 1, last + 1)
            r['tail_kept'] = r['tail_new_frames'] - (E + 1)
            lastf = L[E * SPF:(E + 1) * SPF].astype(np.float64)
            r['endframe_rms'] = float(np.sqrt((lastf ** 2).mean()))
            r['endframe_peak'] = int(np.abs(lastf).max())
            post = np.abs(L[(E + 1) * SPF:].astype(np.int32)).reshape(-1, SPF * 2).max(axis=1)
            r['post_max_at_200'] = int(post[200:].max()) if len(post) > 200 else None
            r['post_max_at_600'] = int(post[600:].max()) if len(post) > 600 else None
            r['post_last_nonzero'] = int(np.nonzero(post)[0][-1]) if post.any() else -1
            r['long_frames'] = len(L) / SPF
        # stats
        pl_, pr_, rl, rr, fa = stats(o)
        r['stats_ok'] = (pl_ == e['PeakLeft'] and pr_ == e['PeakRight'] and rl == e['RmsLeft'] and rr == e['RmsRight']
                         and fa == e['FirstAudibleFrame'] and e['DurationSeconds'] == e['Frames'] / 60.0)
        r['fa_unchanged'] = (fa == t['FirstAudibleFrame'])
        r['peak_changed'] = (e['PeakLeft'] != t['PeakLeft'], e['PeakRight'] != t['PeakRight'])
        r['loopdetected'] = (t['LoopDetected'], e['LoopDetected'])
        r['size_eq'] = (so == 44 + e['Frames'] * 2940)
        rows.append(r)
        print(r, flush=True)
    print('total today', tot_today, 'total oracle', tot_ora)
    json.dump(rows, open(os.path.join(OUT, 'vaudio.json'), 'w'), indent=1, default=str)


if __name__ == '__main__':
    main()
