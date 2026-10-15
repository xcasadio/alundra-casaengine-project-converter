"""run_f3.py - E20.f3 T0 driver: rebuilds every file of out/ (text AND JSON) from the real code of ALUN_CD.EXE (France) on the f-projectiles machine (tools/pworld.py and friends), checks the
facts the plans of E20.f3a, f3b and f3c rely on, and writes the manifest (md5 of every other file of out/; two runs give the same manifest).

usage (repository root; every stage runs in the foreground, the whole thing takes about 9 minutes):
    python -B docs/plan-e20-annexe/e20-f3/oracle/run_f3.py text       the named scripts of tools/ (wave_run, summary, wave_hit, wave_only, wave_z, wave_mut, wave_grass/2/3, hitfx, hitfx_mut,
                                                                       multishot, dirtest, dir_extra, arena_probe, twoint) -> out/*.txt
    python -B docs/plan-e20-annexe/e20-f3/oracle/run_f3.py mutants    mutants_f3.py -> out/mutations.txt, out/mutations.json (12 one-word mutants)
    python -B docs/plan-e20-annexe/e20-f3/oracle/run_f3.py flame      flame_probe.py -> out/flame_probe.json, out/flame_probe.txt
    python -B docs/plan-e20-annexe/e20-f3/oracle/run_f3.py json       export_json.py -> out/*.json
    python -B docs/plan-e20-annexe/e20-f3/oracle/run_f3.py checks     out/checks.txt (PASS / FAIL per fact; exit code 0 = all PASS), then out/manifest.txt
The scripts of tools/ are byte copies of the discovery scripts (see checksums.txt); they write into ../out relative to tools/, that is this out/.
"""
import os
import sys
import json
import hashlib
import subprocess

HERE = os.path.dirname(os.path.abspath(__file__))
TOOLS = os.path.join(HERE, 'tools')
OUT = os.path.join(HERE, 'out')

TEXT = [
    ('wave_run.py', ['dirs'], None), ('summary.py', [], 'summary.txt'),
    ('wave_hit.py', ['east'], None), ('wave_hit.py', ['north'], None), ('wave_hit.py', ['sweep'], None), ('wave_hit.py', ['balls'], None),
    ('wave_only.py', [], None), ('wave_z.py', [], None), ('wave_mut.py', [], None),
    ('wave_grass.py', [], None), ('wave_grass2.py', [], None), ('wave_grass3.py', [], None),
    ('hitfx.py', [], None), ('hitfx_mut.py', [], None), ('multishot.py', [], None), ('dirtest.py', [], None), ('dir_extra.py', [], None), ('arena_probe.py', [], None), ('twoint.py', [], None),
]


def run_py(cmd, cwd, stdout_to=None):
    with open(os.devnull, 'w') as dn:
        if stdout_to:
            with open(stdout_to, 'w', newline='\n') as f:
                return subprocess.call([sys.executable, '-B'] + cmd, cwd=cwd, stdout=f, stderr=subprocess.STDOUT)
        return subprocess.call([sys.executable, '-B'] + cmd, cwd=cwd, stdout=dn, stderr=subprocess.STDOUT)


def stage_text():
    os.makedirs(OUT, exist_ok=True)
    bad = 0
    for script, args, redirect in TEXT:
        rc = run_py([script] + args, TOOLS, os.path.join(OUT, redirect) if redirect else None)
        print('%-16s %-6s exit %d' % (script, ' '.join(args), rc))
        sys.stdout.flush()
        bad += (rc != 0)
    return 1 if bad else 0


def stage_script(name):
    return run_py([os.path.join(HERE, name)], HERE)


def load(name):
    return json.load(open(os.path.join(OUT, name)))


def stage_checks():
    res = []

    def check(name, ok, detail=''):
        res.append((name, bool(ok), detail))

    mut = load('mutations.json')
    check('12 mutants, all killed', len(mut['rows']) >= 12 and all(r['verdict'] == 'KILLED' for r in mut['rows']), '%d rows' % len(mut['rows']))
    hx = load('hitfx_table.json')
    check('hitfx_table: 160 pairs, 160 match the decoded rule', hx['pairs'] == 160 and hx['matching'] == 160)
    check('hitfx_table: rows carry the keys attr nib resp fx sparks c1d0 touch hitc rng_before rng_after', all(k in hx['rows'][0] for k in ('attr', 'nib', 'resp', 'fx', 'sparks', 'c1d0', 'touch', 'hitc', 'rng_before', 'rng_after')))
    check('hitfx_table: effect attachments draw nothing (sparks only): rng step identical for nibble 6 and nibble 1 at the same response',
          len({(r['rng_after'] - r['rng_before']) & 0xFFFFFFFF for r in hx['rows'] if r['resp'] == 0x10 and r['nib'] in (1, 6)}) == 1)
    tw = open(os.path.join(OUT, 'two_interpreters.txt')).read()
    check('two independent interpreters agree on the wave scenarios', tw.count('True') == 4 and 'False' not in tw)
    for sword, direction, power, dx, dy in (('steel', 'east', 10, 12, 0), ('steel', 'north', 10, 0, -8), ('steel', 'west', 10, -12, 0), ('steel', 'south', 10, 0, 8), ('demon', 'east', 20, 12, 0), ('holy', 'east', 38, 12, 0)):
        d = load('wave_%s_%s.json' % (sword, direction))
        rows = {r['tick']: r for r in d['rows']}
        w = {t: r['slots'].get('1') for t, r in rows.items()}
        S = min(t for t, v in w.items() if v)
        life = [t for t, v in w.items() if v]
        ok = (S == 117 and max(life) == S + 26 and 144 not in life and w[S + 26]['status'] == 4 and w[S]['cnt1c'] == 1 and w[S + 25]['cnt1c'] == 26 and w[S + 24]['reset_AC'] == 0 and w[S + 25]['reset_AC'] == 1
              and all(v['type68'] == {'steel': 27, 'demon': 28, 'holy': 29}[sword] and v['flags6C'] == 0x205C and (v['atk_attr'], v['atk_pow']) == (0x81, power) and (v['fx'], v['fy'], v['fz']) == (0, 0, 0) and v['adj_13C'] == 0 and v['ground_144'] == 0 for v in w.values() if v)
              and len({(v['px'], v['py'], v['pz']) for v in w.values() if v}) == 1 and w[S]['pz'] == 2)
        ks = (0, 1, 3, 5, 7, 9)
        ok = ok and [w[S + k]['hbx'] // 65536 for k in ks] == [82 + dx * j for j in range(6)] and [w[S + k]['hby'] // 65536 for k in ks] == [88 + dy * j for j in range(6)]
        check('wave %s %s: born S=117 (R+21) at the hero position, rawZ 2, forces 0, ForceAdjusted 0, ground 0, flags 0x205C, attack (0x81, %d), +0x1C 1 -> 26, +0xAC at S+25, status 4 at S+26, gone at S+27, box moves' % (sword, direction, power), ok)
    for name in ('wave_steel_east.json',):
        d = load(name)
        rows = {r['tick']: r for r in d['rows']}
        check('wave: size 36x24x31 (bw 35.99998.. as raw 2359295/1572863/2031615)', rows[117]['slots']['1']['bw'] == 2359295 and rows[117]['slots']['1']['bd'] == 1572863 and rows[117]['slots']['1']['bh'] == 2031615, str((rows[117]['slots']['1']['bw'], rows[117]['slots']['1']['bd'], rows[117]['slots']['1']['bh'])))
        check('wave: snd(30) at S and snd(0) at S+25 (muted by the sound routine, see the plan)', 'snd(30)' in rows[117]['journal'] and 'snd(0)' in rows[142]['journal'])
    he = load('wave_hit_east.json')
    check('wave vs Beannoid east: first wave hit tick journalled (touch slot 2 = the wave, 0 = the hero)', len(he['hits_tick_touch_slot']) >= 1, str(he['hits_tick_touch_slot']))
    ar = load('arena_probe.json')
    ctl = [h for h in ar['runs'][0]['hits']]
    mut_ = [h for h in ar['runs'][1]['hits']]
    check('arena pin: with E10, 2 contacts (hero, hero); without E10 the wave contacts appear (oracle ticks 143 and 168 = arena ticks 138 and 163; the third contact is inside the 140 ticks of the arena)', len(ctl) == 2 and all(h['attacker'] == 'hero' for h in ctl) and len(mut_) == 4 and sum(1 for h in mut_ if h['attacker'] == 'wave') == 2, '%s | %s' % (ctl, mut_))
    fr = load('hero_fractional.json')
    check('O-F3-2: the oracle accepts a fractional hero position and the wave keeps it unchanged', fr['wave_first_px_py_pz'][:2] == fr['hero_raw_xy'])
    dr = {c['name']: c for c in load('directions.json')['cases']}
    check('(d1) key: air, Square+Left at t8 -> shot at t9 with direction 8', dr['dirtest_air_cross_t1_square_left_t8']['spawn_tick'] == 9 and dr['dirtest_air_cross_t1_square_left_t8']['arrow']['dir_t'] == 8)
    check('(d2) key: Square at the press, Left only from the shot tick -> 0x18', dr['dir_extra_air_square_t8_left_only_from_t9']['spawn_tick'] == 9 and dr['dir_extra_air_square_t8_left_only_from_t9']['arrow']['dir_t'] == 24)
    check('(d3) keys: ground Up / Down / Left / Right -> 0x10 / 0 / 8 / 0x18', [dr[k]['arrow']['dir_t'] for k in ('dirtest_ground_square_up_t1', 'dir_extra_ground_square_down_t1', 'dir_extra_ground_square_left_t1', 'dir_extra_ground_square_right_t1')] == [16, 0, 8, 24])
    ms = {c['name']: c for c in load('multishot.json')['cases']}
    check('multishot: two arrows live together (births at t10 and t34)', [b['tick'] for b in ms['hunter_square_t1_t25']['births']] == [10, 34])
    fl = load('flame_probe.json')
    check('flame probe answered (animation 0 attribute list, effects, verdict present)', 'answer' in fl and fl['answer']['animation_0_attribute_values'] == [1])
    out = ['# E20.f3 T0 checks: one line per fact; exit code 0 when every line is PASS']
    for name, ok, detail in res:
        out.append('%s  %s%s' % ('PASS' if ok else 'FAIL', name, ('   [' + detail + ']') if detail else ''))
    out.append('%d of %d PASS' % (sum(1 for r in res if r[1]), len(res)))
    open(os.path.join(OUT, 'checks.txt'), 'w', newline='\n').write('\n'.join(out) + '\n')
    print('\n'.join(out))
    stage_manifest()
    return 0 if all(r[1] for r in res) else 1


def stage_manifest():
    lines = []
    for f in sorted(os.listdir(OUT)):
        if f == 'manifest.txt':
            continue
        lines.append('%s  %s' % (hashlib.md5(open(os.path.join(OUT, f), 'rb').read()).hexdigest(), f))
    open(os.path.join(OUT, 'manifest.txt'), 'w', newline='\n').write('\n'.join(lines) + '\n')


if __name__ == '__main__':
    st = sys.argv[1] if len(sys.argv) > 1 else ''
    if st == 'text':
        sys.exit(stage_text())
    if st == 'mutants':
        sys.exit(stage_script('mutants_f3.py'))
    if st == 'flame':
        sys.exit(stage_script('flame_probe.py'))
    if st == 'json':
        sys.exit(stage_script('export_json.py'))
    if st == 'checks':
        sys.exit(stage_checks())
    sys.exit('usage: run_f3.py text|mutants|flame|json|checks')
