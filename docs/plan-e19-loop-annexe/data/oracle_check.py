# Cross-checks of the L-b oracle (option B) and the per-track tables.
#   A = harness oracle (oracle/sound/bgm, rule b, crossfade 735, one-shot tail "audible60")
#   B = this script, independently: frames cut from the long renders (long/, a separate harness run with its own stop
#       logic), the crossfade and the one-shot tail rule recomputed here, WAV header written here.
#   C = the unchanged extractor CLI --render-bgm --frames <Frames>, one process per track (renderbgm/); it must equal the
#       harness's no-crossfade run (run-b-raw) byte for byte.
#   D = today's data-extracted/sound/bgm (X2): its data must be a byte prefix of A.
# Writes oracle_sha256.tsv and loops_final.tsv.
import csv, hashlib, os, struct
import numpy as np

SPF = 735
HERE = os.path.dirname(os.path.abspath(__file__))
DE = r'D:\development\repo\alundra-casaengine-project-converter\data-extracted\sound'
LOOPS = os.path.join(HERE, '..', 'loop-disc', 'loops.tsv')


def sha256_file(p):
    h = hashlib.sha256()
    with open(p, 'rb') as f:
        for chunk in iter(lambda: f.read(1 << 20), b''):
            h.update(chunk)
    return h.hexdigest()


def read_wav(p):
    b = open(p, 'rb').read()
    n = int.from_bytes(b[40:44], 'little')
    assert len(b) == 44 + n, p
    return b[:44], np.frombuffer(b[44:], dtype='<i2').reshape(-1, 2)


def header(nframes_stereo):
    data = nframes_stereo * 4
    return (b'RIFF' + struct.pack('<i', 36 + data) + b'WAVE' + b'fmt ' + struct.pack('<ihhiihh', 16, 1, 2, 44100, 44100 * 4, 4, 16)
            + b'data' + struct.pack('<i', data))


def crossfade(x, j1, j2, k=735):
    y = x.astype(np.int64).copy()
    a0, b0 = j2 * SPF - k, j1 * SPF - k
    for i in range(k):
        w = i + 1
        for ch in range(2):
            num = int(x[a0 + i, ch]) * (k - w) + int(x[b0 + i, ch]) * w
            q = (num + k // 2) // k if num >= 0 else -((-num + k // 2) // k)
            y[a0 + i, ch] = q
    return y.astype('<i2')


def tsv(p, key):
    with open(p, newline='') as f:
        return {int(r[key]): r for r in csv.DictReader(f, delimiter='\t')}


def main():
    ev = tsv(os.path.join(HERE, 'long', 'events.tsv'), 'SoundIndex')
    orc = tsv(os.path.join(HERE, 'oracle', 'oracle_b.tsv'), 'SoundIndex')
    lt = tsv(LOOPS, 'idx')
    seam = tsv(os.path.join(HERE, 'seam_b.tsv'), 'idx')
    tails = tsv(os.path.join(HERE, 'tails.tsv'), 'idx')
    today = {e['SoundIndex']: e for e in __import__('json').load(open(os.path.join(DE, 'bgm.json')))}
    rows, lrows = [], []
    for idx in sorted(orc):
        o = orc[idx]
        if o['Status'] != 'exported':
            continue
        fn = 'bgm_%03d.wav' % idx
        pa = os.path.join(HERE, 'oracle', 'sound', 'bgm', fn)
        pr = os.path.join(HERE, 'run-b-raw', 'sound', 'bgm', fn)
        pc = os.path.join(HERE, 'renderbgm', 'bgm_%03d_csharp_render.wav' % idx)
        pd = os.path.join(DE, 'bgm', fn)
        # B: independent derivation from the long render
        hl, xl = read_wav(os.path.join(HERE, 'long', fn))
        jumps = [int(v) for v in ev[idx]['Jumps'].split(',')] if ev[idx]['Jumps'] else []
        ends = [int(v) for v in ev[idx]['Ends'].split(',')] if ev[idx]['Ends'] else []
        if jumps:
            j1, j2 = jumps[0], jumps[1]
            frames_b = j2
            yb = crossfade(xl[:frames_b * SPF], j1, j2)
            looping = True
        else:
            e = ends[0]
            fr = np.abs(xl.astype(np.int32)).max(axis=1)[:(len(xl) // SPF) * SPF].reshape(-1, SPF).max(axis=1)
            aud = fr > 64
            # render on after E until 60 silent frames in a row; keep up to the last audible frame (never below E+1)
            last_aud, run, f = -1, 0, 0
            for f in range(len(fr)):
                if aud[f]:
                    last_aud = f
                    run = 0
                else:
                    run += 1
                if f > e and run >= 60:
                    break
            frames_b = max(e + 1, last_aud + 1)
            yb = xl[:frames_b * SPF].copy()
            j1 = j2 = -1
            looping = False
        bytes_b = header(frames_b * SPF) + yb.tobytes()
        sha_b = hashlib.sha256(bytes_b).hexdigest()
        sha_a = sha256_file(pa)
        ha, xa = read_wav(pa)
        hr, xr = read_wav(pr)
        a_eq_b = sha_a == sha_b
        raw_eq_cli = open(pr, 'rb').read() == open(pc, 'rb').read()
        # where A differs from the raw render (the crossfade region)
        diff_frames = np.nonzero(np.any(xa != xr, axis=1))[0]
        diff_first = int(diff_frames[0]) if len(diff_frames) else -1
        diff_last = int(diff_frames[-1]) if len(diff_frames) else -1
        hd, xd = read_wav(pd)
        prefix_ok = np.array_equal(xa[:len(xd)], xd)
        today_data_sha = hashlib.sha256(xd.tobytes()).hexdigest()
        prefix_sha = hashlib.sha256(xa[:len(xd)].tobytes()).hexdigest()
        t = today[idx]
        rows.append({
            'SoundIndex': idx, 'File': fn, 'Bytes': os.path.getsize(pa), 'SHA256': sha_a,
            'Frames': int(o['Frames']), 'Looping': looping, 'LoopStartFrame': j1 if looping else '',
            'LoopStartSample': j1 * SPF if looping else '', 'J1': j1 if looping else '', 'J2': j2 if looping else '',
            'EndOfTrackFrame': '' if looping else ends[0],
            'TodayFrames': t['Frames'], 'TodayBytes': os.path.getsize(pd), 'TodaySHA256': sha256_file(pd),
            'Changed': sha_a != sha256_file(pd),
            'HarnessEqualsIndependent': a_eq_b, 'RawEqualsRenderBgmCli': raw_eq_cli,
            'CrossfadeChangedStereoFrames': len(diff_frames), 'CrossfadeFirstChangedSample': diff_first, 'CrossfadeLastChangedSample': diff_last,
            'TodayIsBytePrefix': prefix_ok, 'TodayDataSHA256': today_data_sha, 'PrefixDataSHA256': prefix_sha,
        })
        r = lt[idx]
        lrow = {'idx': idx, 'kind': 'loop' if looping else 'one-shot',
                'LS_frame': r['LS_frame'] if looping else '', 'LS_tick': r['LS_tick'] if looping else '',
                'J1': j1 if looping else '', 'J2': j2 if looping else '', 'J3': jumps[2] if looping else '',
                'P1': r['P1_frames'] if looping else '', 'P2': (j2 - j1) if looping else '', 'P3': (jumps[2] - j2) if looping else '',
                'loops_tsv_agrees': (looping and r['J1_frame'] == str(j1) and r['J2_frame'] == str(j2) and r['P3_frames'] == str(jumps[2] - j2))
                                    or (not looping and r['end_frame'] == str(ends[0])),
                'Frames_new': int(o['Frames']), 'Frames_today': t['Frames'], 'Seconds_new': round(int(o['Frames']) / 60, 3),
                'LoopStartFrame': j1 if looping else '', 'LoopStartSample': j1 * SPF if looping else '',
                'LoopEndSample': int(o['Frames']) * SPF if looping else '',
                'EndOfTrackFrame': '' if looping else ends[0], 'TailFrames': '' if looping else int(o['Frames']) - (ends[0] + 1),
                'Bytes_new': os.path.getsize(pa), 'Bytes_today': os.path.getsize(pd),
                'iter1_differs': r['iter1_differs_from_steady_state'], 'used_by_map_entry': r['used_by_map_entry'], 'used_by_0xA7': r['used_by_0xA7']}
        if looping:
            s = seam[idx]
            for c in ('step_p99', 'step_max', 'step_true_at_J2', 'step_seam_noxf', 'step_seam_xf735', 'pre_lastframe_err_rms',
                      'lastframe_signal_rms', 'post_first_gt0', 'post_first_gt64', 'post_err_rms_frame0'):
                lrow[c] = s[c]
        else:
            s = tails[idx]
            lrow['endframe_rms_today_cut'] = s['endframe_rms']
        lrows.append(lrow)

    with open(os.path.join(HERE, 'oracle_sha256.tsv'), 'w', newline='') as f:
        w = csv.DictWriter(f, fieldnames=list(rows[0].keys()), delimiter='\t', lineterminator='\n')
        w.writeheader(); w.writerows(rows)
    keys = []
    for r in lrows:
        for k in r:
            if k not in keys:
                keys.append(k)
    with open(os.path.join(HERE, 'loops_final.tsv'), 'w', newline='') as f:
        w = csv.DictWriter(f, fieldnames=keys, delimiter='\t', lineterminator='\n', restval='')
        w.writeheader(); w.writerows(lrows)

    print('tracks', len(rows))
    print('harness == independent:', sum(r['HarnessEqualsIndependent'] for r in rows))
    print('raw == --render-bgm:', sum(r['RawEqualsRenderBgmCli'] for r in rows))
    print('today is byte prefix:', sum(r['TodayIsBytePrefix'] for r in rows))
    print('changed:', sum(r['Changed'] for r in rows))
    print('loops.tsv agrees:', sum(bool(r['loops_tsv_agrees']) for r in lrows))
    print('crossfade no-op on:', [r['SoundIndex'] for r in rows if r['Looping'] and r['CrossfadeChangedStereoFrames'] == 0])
    print('crossfade region bounds ok:', all((not r['Looping']) or r['CrossfadeChangedStereoFrames'] == 0 or
          (r['CrossfadeFirstChangedSample'] >= (r['Frames'] - 1) * SPF and r['CrossfadeLastChangedSample'] <= r['Frames'] * SPF - 1) for r in rows))
    print('one-shots unchanged-but-for-tail:', [(r['SoundIndex'], r['TodayFrames'], r['Frames']) for r in rows if not r['Looping']])
    tot_new = sum(r['Bytes'] for r in rows); tot_today = sum(r['TodayBytes'] for r in rows)
    print('bytes today %d (%.1f MB, %.1f MiB) new %d (%.1f MB, %.1f MiB) ratio %.3f' % (tot_today, tot_today / 1e6, tot_today / 2**20, tot_new, tot_new / 1e6, tot_new / 2**20, tot_new / tot_today))


if __name__ == '__main__':
    main()
