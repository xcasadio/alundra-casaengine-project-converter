"""run_e0a5.py - E20.e0a5 T0 driver (new for this slice; the only file of this annex that is not a discovery file).

Rebuilds every file of out/ from the real code of ALUN_CD.EXE (France) on the f-projectiles machine (tools/pworld.py and friends, byte copies of the discovery), checks the rows against
the model of the plan (section 1.16.5, value table) and writes the manifest (md5 of every other file of out/; two runs give the same manifest).  Nothing here knows an expected value of
the DLL: every number of the JSON is read from the emulated RAM; the expected values live in the `checks` stage only, which turns a contradiction into a FAIL (exit code 1 = a stop).

usage (run from a scratch copy of this folder, foreground, about 1 minute):
    python -B run_e0a5.py json       the rows of the clock and of the box -> out/birth_clock.json, out/birth_clock.txt, out/trace_*.txt (the pass-by-pass traces of the wave)
    python -B run_e0a5.py checks     out/checks.txt (PASS / FAIL per fact; exit code 0 = all PASS), then out/manifest.txt

Rows of out/birth_clock.json (`rows[]`, one per label; `ticks[]` per tick, `k` = tick - S, S = the tick of the creation, 0 for the record-born entity of a map load):
  WAVE      the sword wave (global sprite 27, steel, east), born in MovePlayer, no script switch               (tools/trace_birth2.py)
  NOSWITCH  the same with the `jal 0x80038AB4` of InitializeEntity replaced by a nop (the mutant that reproduces the DLL's lag)
  SW0 SW1 SW2 SW5 SW1-2 SWD1 SWD2   a script switch injected at S+k (animation 1 of the sprite 27, 16 ticks: SW<k>; SW1-2: animation 1 at S+1 then back to 0 at S+2; SWD<k>: direction 16 at S+k)
  SPR251 SPR3  global sprite 251 (the frame-1 sprite) and the arrow (3) born in MovePlayer                        (tools/trace_kinds.py, scenario move)
  LOAD27    the wave sprite born by a map record at load (the real record spawner 0x8003A1B8 before tick 1)      (tools/trace_kinds.py, scenario load)
Keys per tick: cnt1c (+0x1C), end_AC (+0xAC, the end-of-animation flag; the wave's `reset_AC` of e20-f3), loops_B0 (+0xB0), status (+0x10), anim_c/dir_c (+0x90/+0x94), and for the trace_birth rows
the raw 16.16 words px py pz (+0x114..), hbx hby hbz (+0x1FC..), bw bd bh (+0x214.. , size minus one raw unit as the binary stores it).
"""
import os
import sys
import json
import hashlib

HERE = os.path.dirname(os.path.abspath(__file__))
TOOLS = os.path.join(HERE, 'tools')
OUT = os.path.join(HERE, 'out')
sys.path.insert(0, TOOLS)

ORDER = [('WAVE', None), ('NOSWITCH', None), ('SW0', '0:1'), ('SW1', '1:1'), ('SW2', '2:1'), ('SW5', '5:1'), ('SW1-2', '1:1,2:0'), ('SWD1', '1:0:16'), ('SWD2', '2:0:16')]


def dump(name, obj):
    os.makedirs(OUT, exist_ok=True)
    with open(os.path.join(OUT, name), 'w', newline='\n') as f:
        json.dump(obj, f, indent=None, sort_keys=True, separators=(',', ':'))
        f.write('\n')


def birth_rows():
    import trace_birth2 as B
    rows = []
    for label, sw in ORDER:
        for k in ('SWITCH', 'NOSWITCH'):
            os.environ.pop(k, None)
        if label == 'NOSWITCH':
            os.environ['NOSWITCH'] = '1'
        if sw:
            os.environ['SWITCH'] = sw
        m, tr = B.run_wave('steel', 'east', n=150)
        S = tr.created_at[0]
        ticks = []
        last = {}
        for (t, ps), what, d in tr.rows:
            if what == 'after pass' and ps == 'hits':
                last[t] = d
        for t in range(S, S + 31):
            d = last[t]
            ticks.append(dict(k=t - S, status=d['status'], cnt1c=d['c1c'], end_AC=d['aAC_end'], loops_B0=d['aB0_loops'], anim_c=d['a90_curAnim'], dir_c=d['a94_curDir'], anim_t=d['a88_targetAnim'],
                              dir_t=d['a8C_targetDir'], px=d['p114'], py=d['p118'], pz=d['p11C'], hbx=d['w1FC'], hby=d['w200'], hbz=d['w204'], bw=d['b214'], bd=d['b218'], bh=d['b21C'],
                              frame_shown=d['frame_shown'], cnt_A8=d['aA8_cnt']))
        rows.append(dict(label=label, scenario='wave steel east', source='tools/trace_birth2.py', switch=sw, noswitch=(label == 'NOSWITCH'), S=S, ticks=ticks))
        # the pass-by-pass traces of the discovery (same file names)
        lines = ['# wave steel east: created at tick %d (inside pass %s)' % (S, tr.created_at[1])]
        lines.append('# first rows: state right after the creation (InitializeEntity + first switch), then after every pass of tick S..S+30')
        for r in tr.rows:
            if r[0][0] < S - 1 or r[0][0] > S + 30:
                continue
            lines.append(B.fmt_row(r))
        suffix = ('_NOSWITCH' if label == 'NOSWITCH' else '') + (('_SW' + sw.replace(':', '_').replace(',', '__')) if sw else '')
        os.makedirs(OUT, exist_ok=True)
        with open(os.path.join(OUT, 'trace_wave_steel_east%s.txt' % suffix), 'w', newline='\n') as f:
            f.write('\n'.join(lines) + '\n')
    for k in ('SWITCH', 'NOSWITCH'):
        os.environ.pop(k, None)
    return rows


def kinds_row(label, scenario, spr, nticks):
    import trace_kinds as K
    K.SPR = spr
    m, run, t0 = K.scenario(scenario)
    by = {}
    for t, nm, d in run.log:
        by.setdefault(t, {})[nm] = d
    S = run.created_tick
    ticks = []
    for t in sorted(by):
        if t < max(S, 1) or t > S + nticks:
            continue
        d = by[t]['hits']
        ticks.append(dict(k=t - S, tick=t, status=d['status'], cnt1c=d['c1c'], end_AC=d['end'], loops_B0=d['b0'], frame=d['frame'], cnt_A8=d['cnt'], adir=d['adir']))
    return dict(label=label, scenario='%s sprite %d' % (scenario, spr), source='tools/trace_kinds.py', S=S, ticks=ticks)


def stage_json():
    rows = birth_rows()
    rows.append(kinds_row('SPR251', 'move', 251, 30))
    rows.append(kinds_row('SPR3', 'move', 3, 30))
    rows.append(kinds_row('LOAD27', 'load', 27, 33))
    dump('birth_clock.json', dict(scenario='e0a5 birth clock', rows=rows))
    lines = ['# birth_clock: first tick (k = tick - S) at which each key takes its value; box changes = ticks where (hbx, hby, hbz, bw, bd, bh) differ from the previous tick']
    for r in rows:
        t = r['ticks']
        first_end = next((x['k'] for x in t if x['end_AC'] == 1), None)
        first_st4 = next((x['k'] for x in t if x.get('status') == 4), None)
        first_loop = {}
        for x in t:
            first_loop.setdefault(x['loops_B0'], x['k'])
        chg = []
        for a, b in zip(t, t[1:]):
            if 'hbx' in a and any(a[f] != b[f] for f in ('hbx', 'hby', 'hbz', 'bw', 'bd', 'bh')):
                chg.append(b['k'])
        cnt = [x['cnt1c'] for x in t[:4]]
        lines.append('%-9s S=%-4d end_AC first at k=%s status4 first at k=%s cnt1c(k=0..3)=%s loops_B0 first k per value=%s box changes at k=%s' % (r['label'], r['S'], first_end, first_st4, cnt, first_loop, chg))
        if r['label'] == 'SW0':
            x0 = t[0]
            lines.append('          SW0 box at k=0: hbx=%.1f hby=%.1f hbz=%.1f  size=%d x %d x %d' % (x0['hbx'] / 65536, x0['hby'] / 65536, x0['hbz'] / 65536, (x0['bw'] + 1) >> 16, (x0['bd'] + 1) >> 16, (x0['bh'] + 1) >> 16))
    with open(os.path.join(OUT, 'birth_clock.txt'), 'w', newline='\n') as f:
        f.write('\n'.join(lines) + '\n')
    return 0


def load(name):
    with open(os.path.join(OUT, name)) as f:
        return json.load(f)


def stage_checks():
    res = []

    def check(name, ok, detail=''):
        res.append((name, bool(ok), detail))

    d = load('birth_clock.json')
    R = {r['label']: r for r in d['rows']}
    f3 = json.load(open(os.path.join(HERE, '..', '..', 'e20-f3', 'oracle', 'out', 'wave_steel_east.json')))

    def first(r, key, val):
        return next((x['k'] for x in r['ticks'] if x[key] == val), None)

    def at(r, k):
        return next(x for x in r['ticks'] if x['k'] == k)

    w = R['WAVE']
    check('WAVE: end_AC is 0 up to S+24 and 1 at S+25', first(w, 'end_AC', 1) == 25 and all(x['end_AC'] == 0 for x in w['ticks'] if x['k'] <= 24), 'first=%s' % first(w, 'end_AC', 1))
    check('WAVE: cnt1c is 1, 2, 3 at S, S+1, S+2', [at(w, k)['cnt1c'] for k in (0, 1, 2)] == [1, 2, 3], str([at(w, k)['cnt1c'] for k in (0, 1, 2)]))
    check('WAVE: status 4 first at S+26', first(w, 'status', 4) == 26, 'first=%s' % first(w, 'status', 4))
    box = [(x['hbx'] - x['px'], x['hby'] - x['py'], x['hbz'] - x['pz'], x['bw'], x['bd'], x['bh']) for x in w['ticks']]
    chg = [i for i in range(len(box)) if i == 0 or box[i] != box[i - 1]]
    check('WAVE: the box changes at S, S+1, S+3, S+5, S+7, S+9 (then no change to S+25)', chg[:6] == [0, 1, 3, 5, 7, 9] and all(c > 25 or c in (0, 1, 3, 5, 7, 9) for c in chg), str(chg))
    check('WAVE: hbx - px = -18 / -6 / +6 / +18 / +30 / +42 px at S / S+1 / S+3 / S+5 / S+7 / S+9',
          [(at(w, k)['hbx'] - at(w, k)['px']) // 65536 for k in (0, 1, 3, 5, 7, 9)] == [-18, -6, 6, 18, 30, 42], str([(at(w, k)['hbx'] - at(w, k)['px']) // 65536 for k in (0, 1, 3, 5, 7, 9)]))
    check('WAVE: size 36 x 24 x 31 at S', ((at(w, 0)['bw'] + 1) >> 16, (at(w, 0)['bd'] + 1) >> 16, (at(w, 0)['bh'] + 1) >> 16) == (36, 24, 31), '')
    # the keys of the wave are those of e20-f3: same values relative to S (the f3 oracle runs another harness)
    fr = [r for r in f3['rows'] if r['slots'].get('1')]
    S3 = fr[0]['tick']
    f3k = {r['tick'] - S3: r['slots']['1'] for r in fr}
    same = all(f3k[k]['cnt1c'] == at(w, k)['cnt1c'] and f3k[k]['reset_AC'] == at(w, k)['end_AC'] and (f3k[k]['hbx'] - f3k[k]['px']) == (at(w, k)['hbx'] - at(w, k)['px']) for k in range(0, 28) if k in f3k)
    check('WAVE: cnt1c, reset_AC and hbx - px equal the e20-f3 keys for S..S+27', same, 'S3=%d' % S3)
    n = R['NOSWITCH']
    check('NOSWITCH: end_AC first at S+26 and status 4 at S+27 (the DLL lag)', first(n, 'end_AC', 1) == 26 and first(n, 'status', 4) == 27, 'end=%s st4=%s' % (first(n, 'end_AC', 1), first(n, 'status', 4)))
    nb = [(x['hbx'] - x['px']) for x in n['ticks']]
    nchg = [i for i in range(1, len(nb)) if nb[i] != nb[i - 1]]
    check('NOSWITCH: the box changes at S+2, S+4, ... (not at S+1, S+3)', nchg[:3] == [2, 4, 6], str(nchg[:6]))
    for label, exp in (('SW0', 16), ('SW1', 17), ('SW2', 18), ('SW5', 21), ('SW1-2', 28), ('SWD1', 27), ('SWD2', 28)):
        check('%s: end_AC first at S+%d' % (label, exp), first(R[label], 'end_AC', 1) == exp, 'first=%s' % first(R[label], 'end_AC', 1))
    s0 = at(R['SW0'], 0)
    check('SW0: box of the animation 1 at S: hbx 88, 23 x 23 x 15', (s0['hbx'] // 65536, (s0['bw'] + 1) >> 16, (s0['bd'] + 1) >> 16, (s0['bh'] + 1) >> 16) == (88, 23, 23, 15),
          '%s' % ((s0['hbx'] / 65536, (s0['bw'] + 1) >> 16, (s0['bd'] + 1) >> 16, (s0['bh'] + 1) >> 16),))
    p = R['SPR251']
    check('SPR251: loops_B0 is 0 up to S+3, 1 at S+4, 2 at S+9', [at(p, k)['loops_B0'] for k in (3, 4, 8, 9)] == [0, 1, 1, 2], str([at(p, k)['loops_B0'] for k in (3, 4, 8, 9)]))
    a = R['SPR3']
    check('SPR3 (arrow): loops_B0 is 0 up to S+10 and 1 at S+11', [at(a, k)['loops_B0'] for k in (10, 11)] == [0, 1], str([at(a, k)['loops_B0'] for k in (10, 11)]))
    ld = R['LOAD27']
    check('LOAD27: end_AC first at tick 26 (call number 1 at tick 1; the DLL: tick 27)', first(ld, 'end_AC', 1) == 26 and ld['S'] == 0, 'first=%s S=%s' % (first(ld, 'end_AC', 1), ld['S']))
    check('LOAD27: cnt1c is 1 at tick 1', at(ld, 1)['cnt1c'] == 1, '')
    bad = [c for c in res if not c[1]]
    with open(os.path.join(OUT, 'checks.txt'), 'w', newline='\n') as f:
        for name, ok, detail in res:
            f.write('%s  %s%s\n' % ('PASS' if ok else 'FAIL', name, ('   [' + detail + ']') if detail else ''))
        f.write('%d facts, %d FAIL\n' % (len(res), len(bad)))
    write_manifest()
    return 1 if bad else 0


def write_manifest():
    lines = []
    for fn in sorted(os.listdir(OUT)):
        if fn == 'manifest.txt':
            continue
        with open(os.path.join(OUT, fn), 'rb') as f:
            lines.append('%s  %s' % (hashlib.md5(f.read()).hexdigest(), fn))
    with open(os.path.join(OUT, 'manifest.txt'), 'w', newline='\n') as f:
        f.write('\n'.join(lines) + '\n')


if __name__ == '__main__':
    st = sys.argv[1] if len(sys.argv) > 1 else ''
    if st == 'json':
        sys.exit(stage_json())
    if st == 'checks':
        sys.exit(stage_checks())
    raise SystemExit('usage: run_e0a5.py json|checks')
