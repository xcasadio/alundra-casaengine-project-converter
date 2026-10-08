import csv, json, os, statistics
S = r"C:\Users\casad\AppData\Local\Temp\claude\D--development-repo-alundra-casaengine-project-converter\b00d1a72-420d-4acc-ab34-0c25fbdad3a1\scratchpad"
rows = json.load(open(os.path.join(S, 'loop-data-verify', 'vaudio.json')))
sha_tsv = {int(r['SoundIndex']): r for r in csv.DictReader(open(os.path.join(S, 'loop-data', 'oracle_sha256.tsv')), delimiter='\t')}
seam = {int(r['idx']): r for r in csv.DictReader(open(os.path.join(S, 'loop-data', 'seam_b.tsv')), delimiter='\t')}
lf = {int(r['idx']): r for r in csv.DictReader(open(os.path.join(S, 'loop-data', 'loops_final.tsv')), delimiter='\t')}
loop = [r for r in rows if r['looping']]
one = [r for r in rows if not r['looping']]
print('tracks', len(rows), 'looping', len(loop), 'one-shots', [r['idx'] for r in one])
def allok(k, rs=rows):
    bad = [r['idx'] for r in rs if r.get(k) is not True]
    return 'OK' if not bad else 'BAD %s' % bad
for k in ['today_prefix', 'long_prefix_eq_cli', 'stats_ok', 'fa_unchanged', 'size_eq']:
    print(k, allok(k))
for k in ['J_ok', 'oracle_eq_cli_xf', 'xf_range_ok', 'last_eq_beforeJ1_xf']:
    print(k, allok(k, loop))
print('J1==todayFrames-1 false on', [r['idx'] for r in loop if not r['J1_eq_todayFrames-1']])
print('oracle_eq_cli (one-shots)', allok('oracle_eq_cli', one))
print('sha == oracle_sha256.tsv', [r['idx'] for r in rows if r['sha'] != sha_tsv[r['idx']]['SHA256']] or 'all')
print('frames cli == json', [r['idx'] for r in rows if r['cli_frames'] != r['frames_json']] or 'all')
print('largest', max(rows, key=lambda r: r['bytes'])['idx'], max(r['bytes'] for r in rows), 'max J2', max(r['frames_json'] for r in loop))
print('xf no-op tracks', [r['idx'] for r in loop if r['xf_changed'] == 0])
print('xf changed per track', {r['idx']: (r['xf_changed'], r['xf_maxabs']) for r in loop})
print('raw last == before J1', [r['idx'] for r in loop if r['last_eq_beforeJ1_raw']])
print('lastframe identical', [r['idx'] for r in loop if r['lastframe_identical']])
ge = [r['idx'] for r in loop if r['lastframe_err_rms'] >= r['lastframe_sig_rms'] and not r['lastframe_identical']]
print('lastframe err >= signal', len(ge), ge)
gt = [r['idx'] for r in loop if r['seam_noxf'] > r['p99_xf']]
print('seam_noxf > p99', len(gt), gt)
gt2 = [r['idx'] for r in loop if r['seam_noxf'] > r['p99_xf'] and r['true_J2'] <= r['p99_xf']]
print('seam_noxf > p99 and true step at J2 <= p99', len(gt2), gt2)
print('seam > p99 & seam != natural J1 & true>p99', [(r['idx'], r['true_J2'], r['p99_xf']) for r in loop if r['seam_noxf'] > r['p99_xf'] and r['true_J2'] > r['p99_xf']])
print('p99 raw vs xf differ', [(r['idx'], r['p99_raw'], r['p99_xf']) for r in loop if r['p99_raw'] != r['p99_xf']])
print('seam_xf == natural J1', allok('x', []) if False else all(r['seam_xf'] == r['natural_J1'] for r in loop))
print('median seam_noxf/p99', statistics.median(r['seam_noxf'] / r['p99_xf'] for r in loop), 'median seam_xf/p99', statistics.median(r['seam_xf'] / r['p99_xf'] for r in loop))
print('seam_xf > p99', [(r['idx'], r['seam_xf'], r['p99_xf']) for r in loop if r['seam_xf'] > r['p99_xf']])
print('true_J2 > p99', [(r['idx'], r['true_J2'], r['p99_xf']) for r in loop if r['true_J2'] > r['p99_xf']])
print('seam > max', [r['idx'] for r in loop if r['seam_noxf'] > r['max_xf']])
# compare with seam_b.tsv
mism = []
for r in loop:
    s = seam[r['idx']]
    mine = (r['seam_noxf'], r['p99_xf'], r['max_xf'], r['seam_xf'], r['true_J2'])
    print(r['idx'], 'mine', mine, 'cont', r['cont_first_gt64'], 'lf', round(r['lastframe_err_rms'], 1), round(r['lastframe_sig_rms'], 1), 'silrun', r['max_silent_run'], '| seam_b keys', {k: s[k] for k in list(s)[:0]})
cont = {}
for r in loop:
    v = r['cont_first_gt64']
    cont.setdefault('never' if isinstance(v, str) else ('0' if v == 0 else ('1' if v == 1 else 'late')), []).append((r['idx'], v))
for k, v in cont.items():
    print('cont', k, len(v), v)
print('max silent run', max(loop, key=lambda r: r['max_silent_run'])['idx'], max(r['max_silent_run'] for r in loop))
for r in one:
    print('one-shot', r['idx'], 'E', r['E'], 'today', r['today_frames'], 'new(mine)', r['tail_new_frames'], 'json', r['frames_json'], 'kept', r['tail_kept'],
          'endRMS', round(r['endframe_rms'], 1), 'endpeak', r['endframe_peak'], 'post200', r['post_max_at_200'], 'post600', r['post_max_at_600'], 'lastnz', r['post_last_nonzero'], 'long', r['long_frames'])
print('peak changed L', [r['idx'] for r in rows if r['peak_changed'][0]], 'R', [r['idx'] for r in rows if r['peak_changed'][1]])
