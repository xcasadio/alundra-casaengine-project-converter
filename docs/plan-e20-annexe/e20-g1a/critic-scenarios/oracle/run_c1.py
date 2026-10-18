"""run_c1.py - E20.c1: everything, in order.   usage: python -B tools/run_c1.py     (about 8 minutes; exit code 0 = everything below passed)

 1. D1 re-validation with this folder's copy of the tools (tools/run_all.py -> out/d1/): S-A and S-B equal to the annex field by field.  PASS is required.
 2. the reach sweep (sweep.py -> out/reach-sweep.txt): 364 runs of the real code, each also checked against the independent model of the hit rule.
 3. the scenarios H1..H5 (+ controls) -> out/<name>.txt and out/<name>.json, each cross-checked against the model.
 4. the two-interpreter comparison of every scenario (emu.py vs mips.py) -> out/diff2.txt
 5. the mutation tests and controls -> out/mutations.txt
 6. the load-delay check over every executed instruction -> out/load-delay-check.txt
 7. out/summary.txt (the key facts of every scenario, computed) and out/manifest.txt (md5 of every other file in out/, sorted).
The output is byte-stable: no clock, no address of the host, no hash-ordered iteration.
"""
import hashlib
import os
import subprocess
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
OUT = os.path.normpath(os.path.join(HERE, '..', 'out'))
os.makedirs(OUT, exist_ok=True)
import c1world as C
import scen as S
import sweep
import checks
import datareport


def main():
    rc = 0
    verdicts = []
    # ---- 1. D1 -------------------------------------------------------------------------------------------------------------------
    print('== 1. D1 re-validation (S-A, S-B)')
    sys.stdout.flush()
    os.makedirs(os.path.join(OUT, 'd1'), exist_ok=True)
    with open(os.path.join(OUT, 'd1', 'run_all.stdout.txt'), 'w', encoding='utf-8', newline='\n') as f:
        r = subprocess.run([sys.executable, '-B', os.path.join(HERE, 'run_all.py')], stdout=f, stderr=subprocess.STDOUT).returncode
    comp = open(os.path.join(OUT, 'd1', 'comparison.txt'), encoding='utf-8').read()
    d1_ok = (r == 0) and ('OVERALL: PASS' in comp)
    verdicts.append(('D1 re-validation (S-A 216 values, S-B 216 values, 0 differences)', d1_ok))
    print('   D1:', 'PASS' if d1_ok else 'FAIL')
    sys.stdout.flush()

    # ---- 2. sweep ----------------------------------------------------------------------------------------------------------------
    print('== 2. reach sweep')
    sys.stdout.flush()
    sw = sweep.sweep(OUT)
    reach = sw['reach']
    verdicts.append(('reach sweep: real hit pass equals the independent model on every one of the swept runs (mismatches: %d)' % len(sw['mismatches']), not sw['mismatches']))
    print('   reach: dx in [%d, %d], model mismatches %d' % (sw['near'], sw['reach'], len(sw['mismatches'])))
    sys.stdout.flush()

    # ---- 3. scenarios ------------------------------------------------------------------------------------------------------------
    print('== 3. scenarios')
    sys.stdout.flush()
    specs = S.scenarios(reach)
    near = S.spec('H1-near', 'as H1 with the Beannoid 30 px east: the box of the first frame (offset 0, 29 px wide) already overlaps it, so the contact is on tick 1, the tick of the press; '
                  "the Beannoid body box does not touch the hero", 30, holds={1: S.SQ})
    both = S.spec('H1-both', 'as H1 with the Beannoid 20 px east: contact on tick 1 in BOTH directions (the swing box hits the Beannoid and the Beannoid body box touches the hero)', 20, holds={1: S.SQ})
    specs.insert(1, near)
    specs.insert(2, both)
    summary = []
    cov = set()
    results = {}
    model_ok = True
    for sp in specs:
        res = C.run(sp)
        S.write_scenario(res, OUT)
        results[sp['name']] = res
        cov |= res['machine'].cov
        mv = S.model_vs_real(res)
        this_ok = all(x[3] for x in mv)
        model_ok &= this_ok
        summary.append('===== %s: %s' % (sp['name'], sp['desc']))
        summary += [l for l in S.describe(res)[1:]]
        for ex in res['experiments']:
            c = ex['context']
            summary.append('ComputeNewHp(target slot %d) at tick %d (inside the D handler stub): attack attr 0x%02X power %d, latched hero animation %d, response %s, target HP %d' % (
                ex['slot'], ex['tick'], c['attack_attr'], c['attack_power'], c['latched_anim'], c['response'], c['target_hp_before']))
            for v in ('empty', 'filled', 'live'):
                x = ex['variants'][v]
                eq = ' + '.join('[%d]=%s' % (q['k'], 'null' if not q['ptr'] else 'power %s' % [a[1] for a in q['attacks']]) for q in x['equipment'])
                summary.append('   equipment %-6s (%s): HP %d -> %d (damage %d), returns %d' % (v, eq, x['hp_before'], x['hp_after'], x['damage'], x['returns']))
        summary.append('')
        print('   %-8s done, model-vs-real %s' % (sp['name'], 'EQUAL' if this_ok else 'DIFFERENT'))
        sys.stdout.flush()
    verdicts.append(('scenarios: the real hit ticks equal the independent model, for every ordered pair of entities of every scenario', model_ok))

    # ---- 4. two interpreters -----------------------------------------------------------------------------------------------------
    print('== 4. two-interpreter comparison')
    sys.stdout.flush()
    dl, d_ok, c2 = checks.diff2(specs)
    cov |= c2
    open(os.path.join(OUT, 'diff2.txt'), 'w', encoding='utf-8', newline='\n').write(
        '# every scenario run on emu.py (the oracle) and on mips.py (a separate interpreter, via interp2.py), tick by tick\n' + '\n'.join(dl) + '\n')
    verdicts.append(('two interpreters agree on every scenario (rows, slots after every tick, journal, experiments, RNG)', d_ok))
    print('\n'.join('   ' + l[:160] for l in dl))
    sys.stdout.flush()

    # ---- 5. mutations ------------------------------------------------------------------------------------------------------------
    print('== 5. mutation tests')
    sys.stdout.flush()
    ml, m_ok, c3 = checks.run_mutations(reach)
    cov |= c3
    open(os.path.join(OUT, 'mutations.txt'), 'w', encoding='utf-8', newline='\n').write(
        '# one-word patches of the emulated binary (RAM only) with the expected change written beforehand in tools/checks.py; CAUGHT = the expected change happened\n' + '\n'.join(ml) + '\n')
    verdicts.append(('mutation tests: every mutation caught, every control unchanged', m_ok))
    print('\n'.join('   ' + l.split('\n')[0][:150] for l in ml))
    sys.stdout.flush()

    # ---- 6. load delay -----------------------------------------------------------------------------------------------------------
    print('== 6. load-delay check')
    ll, l_ok = checks.load_delay(cov)
    open(os.path.join(OUT, 'load-delay-check.txt'), 'w', encoding='utf-8', newline='\n').write('\n'.join(ll) + '\n')
    verdicts.append(('load-delay hazard check over every executed instruction', l_ok))
    print('   ' + ll[0])
    sys.stdout.flush()

    datareport.write(OUT)

    # ---- 7. summary, manifest ----------------------------------------------------------------------------------------------------
    head = ['# E20.c1 hit-pass oracle: summary (every value computed by the emulated binary; see notes.md for the setup and the limits)',
            '# reach of the basic-sword ground swing at dy = 0: dx in [%d, %d] px (target x - hero x); H2 uses dx = %d' % (sw['near'], sw['reach'], reach + 1),
            '# Y window (dy = hero y - target y) at dx 20/40/55: %s' % ('; '.join('dx %d: %s' % (k, sw['ywin'][k]) for k in sorted(sw['ywin']))),
            '# the Beannoid body box touches the swinging hero (dy = 0) for dx in [%d, %d]' % (min(d for d in sw['th'] if sw['th'][d]), max(d for d in sw['th'] if sw['th'][d])),
            '', '## verdicts'] + ['%s  %s' % ('PASS' if ok else 'FAIL', t) for t, ok in verdicts] + ['']
    open(os.path.join(OUT, 'summary.txt'), 'w', encoding='utf-8', newline='\n').write('\n'.join(head + summary) + '\n')
    names = []
    for root, dirs, files in os.walk(OUT):
        dirs.sort()
        for fn in sorted(files):
            p = os.path.join(root, fn)
            rel = os.path.relpath(p, OUT).replace('\\', '/')
            if rel != 'manifest.txt':
                names.append((rel, hashlib.md5(open(p, 'rb').read()).hexdigest()))
    names.sort()
    open(os.path.join(OUT, 'manifest.txt'), 'w', encoding='utf-8', newline='\n').write('\n'.join('%s  %s' % (h, n) for n, h in names) + '\n')
    print('== verdicts')
    for t, ok in verdicts:
        print('   %s  %s' % ('PASS' if ok else 'FAIL', t))
        rc |= (not ok)
    print('exit code', rc)
    return rc


if __name__ == '__main__':
    sys.exit(main())
