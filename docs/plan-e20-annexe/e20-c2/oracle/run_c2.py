"""run_c2.py - E20.c2-0: everything, in order.   usage: python -B docs/plan-e20-annexe/e20-c2/oracle/run_c2.py     (exit code 0 = every verdict below is PASS)

 1. D1 gate: tools/run_all.py (the copied base) -> out/d1/; S-A and S-B 216 values each, 0 differences, and every file equal to the one of E20.c1b's out/d1 (read only).
 2. H3 of E20.c1: re-run with the copied tools (c1world.run) and compared to the stored e20-c1/oracle/out/H3.json; then hworld's A1 (H3 extended to 200 ticks): its first 45 rows must equal H3 on
    every common column, and the tick-18 row (the hurt) in particular.  A difference STOPS the run (exit code 2).
 3. the kept scenarios on emu.py -> out/<name>.txt / .json (compact, column-major), the independent model (hmodel.py) on every run -> out/model.txt.
 4. the same runs through the second interpreter (mips.py via interp2.py), compared with the first (rows, start state, slots after every tick, journal, RNG word) -> out/diff2.txt.
 5. mutation tests and controls (hmut.py) -> out/mutations.txt.
 6. instruction coverage, load-delay check, disassembly -> out/coverage.txt, load-delay-check.txt, dis.txt; out/summary.txt (verdicts + key values); out/manifest.txt (md5 of every other file).
The output is byte-stable: no clock, no host path, no hash-ordered iteration.  The elapsed times are printed, not written.
"""
import hashlib
import json
import os
import subprocess
import sys
import time

HERE = os.path.dirname(os.path.abspath(__file__))
TOOLS = os.path.join(HERE, 'tools')
sys.path.insert(0, HERE)
sys.path.insert(0, TOOLS)
OUT = os.path.join(HERE, 'out')
os.makedirs(OUT, exist_ok=True)
ANNEX = os.path.normpath(os.path.join(HERE, '..', '..'))
C1_OUT = os.path.join(ANNEX, 'e20-c1', 'oracle', 'out')
C1B_D1 = os.path.join(ANNEX, 'e20-c1b', 'oracle', 'out', 'd1')

import c1world as C
import scen as S
import kworld as K
import kcheck
import lib
import hworld as H
import hscen as HS
import hmodel as HM
import hmut

T0 = time.time()


def log(msg):
    print('[%6.1fs] %s' % (time.time() - T0, msg))
    sys.stdout.flush()


def w(name, text):
    with open(os.path.join(OUT, name), 'w', encoding='utf-8', newline='\n') as f:
        f.write(text if text.endswith('\n') else text + '\n')


def dumps(x):
    return json.dumps(x, sort_keys=True, default=str)


def norm(x):
    return json.loads(json.dumps(x, default=str))


def md5(path):
    return hashlib.md5(open(path, 'rb').read()).hexdigest()


def fin(res):
    """keep what the later stages need and drop the 2 MB machine"""
    m = res.pop('machine')
    res['cov'] = set(m.cov)
    res['journal_str'] = dumps(m.journal)
    res['rng_end'] = m.peek(C.RNG)
    res['slot_md5'] = hashlib.md5(b''.join(res['slotbytes'])).hexdigest()
    res['slotbytes'] = None
    return res


# ---- 1. D1 gate ------------------------------------------------------------------------------------------------------------------------
def stage_d1(verdicts):
    log('1. D1 gate (S-A, S-B)')
    os.makedirs(os.path.join(OUT, 'd1'), exist_ok=True)
    if '--reuse-d1' in sys.argv:                  # development shortcut only: the files already in out/d1 are checked, run_all.py is not re-run
        r = 0
    else:
        with open(os.path.join(OUT, 'd1', 'run_all.stdout.txt'), 'w', encoding='utf-8', newline='\n') as f:
            r = subprocess.run([sys.executable, '-B', os.path.join(TOOLS, 'run_all.py')], stdout=f, stderr=subprocess.STDOUT).returncode
    comp = open(os.path.join(OUT, 'd1', 'comparison.txt'), encoding='utf-8').read()
    n216 = comp.count('VERDICT PASS: 216 values compared, 0 differences')
    ok = (r == 0) and ('OVERALL: PASS' in comp) and n216 == 2
    verdicts.append(('D1 gate: S-A and S-B, 216 values each, 0 differences (tools/run_all.py of the copied base, exit code %d)' % r, ok))
    diffs = []
    n_same = 0
    for fn in sorted(os.listdir(C1B_D1)):
        if fn == 'run_all.stdout.txt':
            continue
        p1, p2 = os.path.join(OUT, 'd1', fn), os.path.join(C1B_D1, fn)
        if not os.path.exists(p1) or md5(p1) != md5(p2):
            diffs.append(fn)
        else:
            n_same += 1
    verdicts.append(('D1 gate: the %d files of out/d1 equal E20.c1b\'s out/d1 byte for byte (differences: %s)' % (n_same, diffs or 'none'), not diffs))
    log('   D1: %s, files equal to c1b: %d, different: %s' % ('PASS' if ok else 'FAIL', n_same, diffs))
    return ok and not diffs


# ---- 2. H3 --------------------------------------------------------------------------------------------------------------------------------
HERO_TARGET_COLS = [n for n, _o, _s in C.E_FIELDS] + ['pos_z_dll', 'atk_attr', 'atk_power', 'touching_slot']


def stage_h3(verdicts, a1):
    log('2. H3 of E20.c1 and A1')
    stored = json.load(open(os.path.join(C1_OUT, 'H3.json'), encoding='utf-8'))
    spec_h3 = [s for s in S.scenarios(0) if s['name'] == 'H3'][0]
    res = C.run(spec_h3)
    same_rows = norm(res['rows']) == stored['rows']
    same_pre = norm(res['pre']) == stored['pre']
    same_exp = norm(res['experiments']) == stored['experiments']
    ok_a = same_rows and same_pre and same_exp
    verdicts.append(('H3 re-run with the copied tools equals e20-c1/oracle/out/H3.json (rows %s, start state %s, ComputeNewHp experiments %s)' % (
        'equal' if same_rows else 'DIFFERENT', 'equal' if same_pre else 'DIFFERENT', 'equal' if same_exp else 'DIFFERENT'), ok_a))
    lines = ['# H3 of E20.c1 re-run with this folder\'s copy of the tools (c1world.run) against e20-c1/oracle/out/H3.json, then hworld\'s A1 against the same file',
             'H3 re-run: rows %s, start state %s, experiments %s' % (same_rows, same_pre, same_exp)]
    # A1 (hworld) against the stored H3: every column of the hero and of the target, the RNG word, the latch, the journal without the handler stubs
    diffs = []
    ncmp = 0
    for i in range(45):
        sr = stored['rows'][i]
        mr = a1['rows'][i]
        for who, a, b in [('hero', sr['hero'], mr['hero'])] + [('target%d' % k, sr['targets'][k], mr['targets'][k]) for k in range(len(sr['targets']))]:
            for c in HERO_TARGET_COLS:
                ncmp += 1
                if a[c] != b[c]:
                    diffs.append((sr['tick'], who, c, a[c], b[c]))
        for key in ('rng', 'latch_80146DF8'):
            ncmp += 1
            if sr[key] != mr[key]:
                diffs.append((sr['tick'], key, sr[key], mr[key]))
        ncmp += 1
        if [list(x) for x in norm([j for j in sr['journal_raw'] if j[0] != 'handler'])] != [list(x) for x in norm(mr['journal_raw'])]:
            diffs.append((sr['tick'], 'journal'))
    pre_diffs = [(c, stored['pre']['hero'][c], a1['pre']['hero'][c]) for c in HERO_TARGET_COLS if stored['pre']['hero'][c] != a1['pre']['hero'][c]]
    ok_b = not diffs and not pre_diffs
    lines.append('A1 rows 1..45 against H3: %d values compared (hero and target columns %d, RNG word, latch, journal), differences: %s; start state differences: %s' % (ncmp, len(HERO_TARGET_COLS), diffs[:5] or 'none', pre_diffs or 'none'))
    t18_diffs = [d for d in diffs if d[0] == 18]
    r18 = a1['rows'][17]['hero']
    lines.append('A1 tick 18 (the hurt) against H3 tick 18: differences %s; row: anim_target 0x%X, dir_target %d, HP %d, +0x1CC %d, force_x %d, journal %s' % (
        t18_diffs or 'none', r18['anim_target'], r18['dir_target'], r18['hp'], r18['c1cc'], r18['force_x'], a1['rows'][17]['journal']))
    verdicts.append(('A1 rows 1..45 equal H3 of E20.c1 on every common column (%d values); the tick-18 row equals H3\'s' % ncmp, ok_b and not t18_diffs))
    w('H3-equality.txt', '\n'.join(lines))
    log('   H3 re-run %s; A1 vs H3 %s (%d values), tick 18 %s' % ('equal' if ok_a else 'DIFFERENT', 'equal' if ok_b else 'DIFFERENT', ncmp, 'equal' if not t18_diffs else 'DIFFERENT'))
    if t18_diffs:
        log('STOP: the A1 tick-18 row differs from H3: %s' % t18_diffs)
        return False
    return ok_a and ok_b


# ---- 3-4. scenarios, model, second interpreter ----------------------------------------------------------------------------------------
def stage_scenarios(verdicts):
    scs = HS.scenarios()
    nruns = sum(len(sc['runs']) for sc in scs)
    nticks = sum(r['n'] for sc in scs for r in sc['runs'])
    log('3. scenarios on emu.py: %d scenarios, %d runs, %d ticks' % (len(scs), nruns, nticks))
    R, MR, per = {}, {}, []
    for sc in scs:
        results, mres = [], []
        for sp in sc['runs']:
            res = H.run(sp, 'emu', capture=True)
            mr = HM.check(res)
            fin(res)
            R[sp['name']] = res
            MR[sp['name']] = mr
            results.append(res)
            mres.append(mr)
        HS.write_scenario(sc, results, mres, OUT)
        per.append((sc['name'], len(sc['runs']), sum(r['spec']['n'] for r in results), sc['info']))
        log('   %-3s %2d runs %4d ticks%s' % (sc['name'], len(sc['runs']), per[-1][2], '  (informational)' if sc['info'] else ''))
    return scs, R, MR, per


def stage_model(verdicts, scs, R, MR):
    L = ['# the independent model (hmodel.py) on every run: one line per rule; a rule holds when the model equals the oracle on every tick it applies to']
    nrules = 0
    nbad = 0
    for sc in scs:
        for sp in sc['runs']:
            L.append('== %s (%d ticks)%s' % (sp['name'], sp['n'], '  [informational scenario]' if sc['info'] else ''))
            for name, ok, detail in MR[sp['name']]:
                nrules += 1
                nbad += (not ok)
                L.append('   %s  %s%s' % ('HOLDS    ' if ok else 'DIFFERENT', name, ('  -> ' + detail) if detail and not ok else ''))
    w('model.txt', '\n'.join(L))
    verdicts.append(('independent model equals the oracle on every rule of every run (%d rule checks over %d runs, %d differences)' % (nrules, len(R), nbad), nbad == 0))
    log('   model: %d rule checks, %d differences' % (nrules, nbad))
    return nbad == 0


def stage_diff2(verdicts, scs, R):
    log('4. second interpreter (mips.py via interp2.py) on every run')
    L = ['# every run of every scenario on emu.py (the oracle) and on mips.py (a separately written interpreter, via interp2.py, with the same tick dispatcher): rows, start state, the hero + target slots after every tick, journal, RNG word']
    ok = True
    ticks = 0
    for sc in scs:
        for sp in sc['runs']:
            a = R[sp['name']]
            b = H.run(sp, 'mips', capture=True)
            fin(b)
            ticks += sp['n']
            same_rows = dumps(a['rows']) == dumps(b['rows'])
            same_pre = dumps(a['pre']) == dumps(b['pre'])
            same_meta = dumps({k: v for k, v in a['meta'].items()}) == dumps({k: v for k, v in b['meta'].items()})
            same_slots = a['slot_md5'] == b['slot_md5']
            same_j = a['journal_str'] == b['journal_str']
            same_rng = a['rng_end'] == b['rng_end']
            this = same_rows and same_pre and same_meta and same_slots and same_j and same_rng
            ok &= this
            L.append('%-18s %3d ticks: rows %s, start state %s, meta %s, slots (every tick) %s, journal %s, RNG word %s -> %s' % (
                sp['name'], sp['n'], 'equal' if same_rows else 'DIFFERENT', 'equal' if same_pre else 'DIFFERENT', 'equal' if same_meta else 'DIFFERENT', 'equal' if same_slots else 'DIFFERENT',
                'equal' if same_j else 'DIFFERENT', 'equal' if same_rng else 'DIFFERENT', 'OK' if this else 'FAIL'))
    L.append('TOTAL: %d runs, %d ticks through each interpreter: %s' % (len(R), ticks, 'all identical' if ok else 'DIFFERENCES'))
    w('diff2.txt', '\n'.join(L))
    verdicts.append(('two interpreters (emu.py, mips.py) agree on every run (rows, slots after every tick, journal, RNG), %d runs / %d ticks' % (len(R), ticks), ok))
    log('   diff2: %s' % ('identical' if ok else 'DIFFERENT'))
    return ok


# ---- 5. mutations ------------------------------------------------------------------------------------------------------------------------
def stage_mut(verdicts):
    log('5. mutation tests')
    ml, mok, mres = hmut.run()
    w('mutations.txt', '\n'.join(ml))
    n_mut = sum(1 for m in hmut.mutations() if not m.get('control'))
    verdicts.append(('mutation tests: every mutation caught (predicate written beforehand + independent model disagrees), every control as expected (%d mutations, 1 patch control, 1 dispatcher check)' % n_mut, mok))
    log('   mutations: %s' % ('PASS' if mok else 'FAIL'))
    return mok


# ---- 6. coverage, load delay, disassembly, summary, manifest ----------------------------------------------------------------------------
RANGES = [
    ('damage entry 0x80031A68 (toucher branch, hazard branch, cost table, floor)', 0x80031A68, 0x80031B50, True),
    ('MovePlayer from the CheckAndExecuteWarp call to the death test 0x80031B94-0x80031CD0 (frozen exit, locked branch, 0x20 compare, normal path, hurt block)', 0x80031B94, 0x80031CD0, False),
    ('UpdateItemEffectState regeneration block, gates and loop 0x80030C20-0x80030D40', 0x80030C20, 0x80030D40, True),
    ('UpdateItemEffectState whole body 0x800307E8-0x80030D40', 0x800307E8, 0x80030D40, False),
    ('MovePlayer epilogue END 0x80032914-0x80032938 (UpdateItemEffectState, SetPlayerHpMax, SetPlayerHp)', 0x80032914, 0x80032938, True),
    ('SetPlayerHpMax 0x8004DC68 and SetPlayerHp 0x8004DD30 and the getters 0x8004DC50 / 0x8004DD18', 0x8004DC50, 0x8004DD84, False),
    ('state 0x1C handler 0x80032558-0x800325A4 and its Idle flip 0x8003270C-0x8003272C', 0x80032558, 0x800325A4, False),
    ('state 0x31 handler 0x80032680-0x8003268C', 0x80032680, 0x8003268C, False),
    ('states 0x3A / 0x3B handler 0x80031DB4-0x80031DF8 (water)', 0x80031DB4, 0x80031DF8, False),
    ('lists pass draw-list part 0x800385C0-0x80038620 (the blink)', 0x800385C0, 0x80038620, False),
    ('IncreaseHpMaxAndCreateEffect 0x800333AC (vessel, E2)', 0x800333AC, 0x800333E8, False),
]
# instructions that no scenario can reach, with the reason (checked against the coverage; the verdict requires every OTHER instruction of the three mandated ranges)
EXPECTED_UNREACHED = {
    (0x80030D20, 0x80030D20): 'regeneration cap `HP + gain > HpMax -> HpMax`: a gain above 1 needs two regenerating records, and only the accessory record carries a non-zero byte 2 (armour and weapon records carry 0)',
}


def merged(miss):
    out, i = [], 0
    while i < len(miss):
        j = i
        while j + 1 < len(miss) and miss[j + 1] == miss[j] + 4:
            j += 1
        out.append((miss[i], miss[j]))
        i = j + 1
    return out


def stage_cov(verdicts, R):
    covs = set()
    for r in R.values():
        covs |= r['cov']
    L = ['# executed instructions of the functions under test (union over the baseline runs of every scenario, emu.py); an instruction is listed as not executed when no run reached it']
    cov_ok = True
    for name, a, b, mandated in RANGES:
        miss = [pc for pc in range(a, b, 4) if pc not in covs]
        L.append('%-150s %4d of %4d executed' % (name, (b - a) // 4 - len(miss), (b - a) // 4))
        unexplained = []
        for lo, hi in merged(miss):
            why = [v for (x, y), v in EXPECTED_UNREACHED.items() if x <= lo and hi <= y]
            L.append('      not executed: 0x%08X%s%s' % (lo, '' if lo == hi else '-0x%08X' % hi, ('   [unreachable with the real records: %s]' % why[0]) if why else ''))
            if not why:
                unexplained.append((lo, hi))
        if mandated and unexplained:
            cov_ok = False
    w('coverage.txt', '\n'.join(L))
    verdicts.append(('coverage: every instruction of 0x80031A68, of the regeneration block 0x80030C20-0x80030D40 and of the END epilogue executed (one documented unreachable instruction, 0x80030D20)', cov_ok))
    ll, lok = kcheck.load_delay(covs)
    w('load-delay-check.txt', '\n'.join(ll))
    verdicts.append(('load-delay hazard check over every executed instruction', lok))
    dis = []
    for name, a, b, _m in RANGES:
        dis.append('## %s' % name)
        for ins in lib.dis(a, (b - a) // 4):
            dis.append('%08X  %08x  %-8s %s' % (ins.address, lib.u32(ins.address), ins.mnemonic, ins.op_str))
        dis.append('')
    w('dis.txt', '\n'.join(dis))
    log('   coverage %s, load delay %s' % ('complete' if cov_ok else 'INCOMPLETE', 'PASS' if lok else 'FAIL'))


def stage_summary(verdicts, per, R):
    head = ['# E20.c2-0 hurt-hero oracle: summary (every value computed by the emulated binary; see notes.md for the layout, the setup and the limits)', '', '## verdicts']
    head += ['%s  %s' % ('PASS' if ok else 'FAIL', t) for t, ok in verdicts]
    head += ['', '## scenarios (kept: A1 A2 A4 A6 B1 B2 D1 E2 E3 F1 F2 F4 F5 G; informational: C1 E1)']
    head += ['%-3s %2d runs %5d ticks%s' % (n, r, t, '  (informational)' if i else '') for n, r, t, i in per]
    head += ['total %d runs, %d ticks' % (sum(p[1] for p in per), sum(p[2] for p in per)), '', '## key values']
    w('summary.txt', '\n'.join(head + HS.summary(R)))


def stage_manifest():
    names = []
    for root, dirs, files in os.walk(OUT):
        dirs.sort()
        for fn in sorted(files):
            p = os.path.join(root, fn)
            rel = os.path.relpath(p, OUT).replace('\\', '/')
            if rel != 'manifest.txt':
                names.append((rel, md5(p)))
    names.sort()
    text = '\n'.join('%s  %s' % (h, n) for n, h in names)
    prev = None
    mp = os.path.join(OUT, 'manifest.txt')
    if os.path.exists(mp):
        prev = open(mp, encoding='utf-8').read().strip()
    w('manifest.txt', text)
    return prev, text.strip(), len(names)


def main():
    verdicts = []
    rc = 0
    d1_ok = stage_d1(verdicts)
    scs, R, MR, per = stage_scenarios(verdicts)
    if not stage_h3(verdicts, R['A1']):
        w('summary.txt', 'STOP: the A1 tick-18 row differs from H3 of E20.c1 (see H3-equality.txt)')
        print('STOP')
        return 2
    if not d1_ok:
        log('STOP: the D1 gate moved')
        w('summary.txt', 'STOP: the D1 gate moved (see out/d1/comparison.txt)')
        return 2
    stage_model(verdicts, scs, R, MR)
    stage_diff2(verdicts, scs, R)
    stage_mut(verdicts)
    stage_cov(verdicts, R)
    stage_summary(verdicts, per, R)
    prev, cur, n = stage_manifest()
    print('== verdicts')
    for t, ok in verdicts:
        print('   %s  %s' % ('PASS' if ok else 'FAIL', t))
        rc |= (not ok)
    if prev is None:
        print('manifest: %d files, first run in this folder (nothing to compare with)' % n)
    else:
        print('manifest: %d files, %s the manifest of the previous run' % (n, 'IDENTICAL to' if prev == cur else 'DIFFERENT from'))
    print('total %.1f s; exit code %d' % (time.time() - T0, rc))
    return rc


if __name__ == '__main__':
    sys.exit(main())
