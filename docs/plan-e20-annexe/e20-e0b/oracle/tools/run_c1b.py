"""run_c1b.py - E20.c1b: everything, in order.   usage: python -B tools/run_c1b.py     (exit code 0 = D1 passed and every check below passed)

 1. D1 re-validation with this folder's copy of the tools (tools/run_all.py -> out/d1/): S-A and S-B equal to the annex, 216 values each, 0 differences.
 2. K1  the equipment records 0x80146E00 and the 255-byte sheet 0x80127008 (real UpdateItemEffectState) for 6 asked + 5 extra inventories, the independent model, the dependency analysis.
 3. K2  the real ComputeNewHp / ResolveBalanceTarget: hero state, A10 state, non-hero attacker, hero as target; the independent model on every line.
 4. K3  the real handlers 0x71, 0x86, 0x87, 0x95 on a synthetic program state; direct call and the real program runner; the independent model.
 5. K1, K2, K3 again with the second interpreter (mips.py via interp2.py): the output text must be identical.
 6. mutation tests and controls.
 7. instruction coverage, load-delay check, disassembly of the functions under test, summary, manifest.
Output is byte-stable (no clock, no host path, no hash-ordered iteration).
"""
import hashlib
import json
import os
import subprocess
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
OUT = os.path.normpath(os.path.join(HERE, '..', 'out'))
os.makedirs(OUT, exist_ok=True)
import kworld as K
import k1
import k2
import k3
import kmut
import kcheck
import lib


def w(name, text):
    with open(os.path.join(OUT, name), 'w', encoding='utf-8', newline='\n') as f:
        f.write(text if text.endswith('\n') else text + '\n')


def jdump(x):
    return json.dumps(x, sort_keys=True, indent=1, default=str)


def get(entries, label, hp):
    for lab, x in entries:
        if lab == label and x['hp_before'] == hp:
            return x
    raise KeyError((label, hp))


def key_tables(r1, r2, R):
    L = ['## K1 key values (records: blob offset / item id; sheet[3..13] = the 11 responses)']
    for name, inv in K.INV.items():
        r = r1[name]
        recs = []
        for rec in r['records']:
            if rec['ptr']:
                recs.append('[%d] blob+%d item %d attacks %s' % (rec['k'], rec['blob_offset'], rec['item_word'] - 0x1E, rec['attacks']))
            else:
                recs.append('[%d] null' % rec['k'])
        sh = bytes.fromhex(r['sheet'])
        L.append('%-9s %s | sheet[3..13] %s | boots %d%s' % (name, '; '.join(recs), ' '.join('%02X' % b for b in sh[3:14]), r['boots'], '' if name in K.REQUIRED else '   (extra)'))
    L += ['', '## K2 key values (HP of the Beannoid after ComputeNewHp; natural record response 0x10; target HP 100 unless stated)']
    L.append('%-9s %-34s %-26s %-18s %-30s %-24s %-14s %s' % ('inventory', 'HERO real state', 'natural HP100 / HP9', 'resp 0/40/80/C0', 'no-0x80 attr / power16 / nibble4', 'A10 natural HP100 / HP9', 'NH 0x01 / 0x81', 'HT hero 0x01p16 / 0x81p16'))
    for name, inv in K.INV.items():
        res = r2[name]
        H = res['HERO']
        nat = get(H, 'natural record, response 0x10', 100)
        nat9 = get(H, 'natural record, response 0x10', 9)
        rs = [get(H, 'copy of the record with response[nibble 1] = 0x%02X' % v, 100)['hp_after'] for v in (0, 0x40, 0x80, 0xC0)]
        ne1 = get(H, 'attack WITHOUT bit 0x80 (synthetic attr 0x01, power 1)', 100)
        ne16 = get(H, 'attack WITHOUT bit 0x80 (synthetic attr 0x01, power 16)', 100)
        n4 = get(H, 'attack nibble 4 (synthetic attr 0x84, power 2): response[4] = 0x40', 100)
        if res['A10'] == 'same as HERO':
            a10 = 'same as real'
        else:
            a10 = '%d / %d' % (get(res['A10'], 'natural record, response 0x10', 100)['hp_after'], get(res['A10'], 'natural record, response 0x10', 9)['hp_after'])
        nh = res['NH']
        nh1 = get(nh, "A's own attack (attr 0x01: no equipment bit)", 100)['hp_after']
        nh81 = get(nh, 'A attack := synthetic attr 0x81 power 1 (equipment bit), latch as is', 100)['hp_after']
        ht = res['HT']
        ht1 = get(ht, 'A attack := synthetic attr 0x01 power 16', 200)['hp_after']
        ht81 = get(ht, 'A attack := synthetic attr 0x81 power 16 (equipment term, latch as is)', 200)['hp_after']
        meta = res['meta']
        L.append('%-9s anim 0x%02X p%-2d latch %-3d  %3d / %-3d (dmg %d)  %-16s %3d / %3d / %3d        %-24s %3d / %-3d     %3d / %3d' % (
            name, meta['hero_anim'], meta['atk_power'], meta['latch'], nat['hp_after'], nat9['hp_after'], nat['damage'], '/'.join('%d' % v for v in rs), ne1['hp_after'], ne16['hp_after'], n4['hp_after'], a10, nh1, nh81, ht1, ht81))
    L += ['', '## K3 key values (Result word; the returned instruction sizes are 0x71: 1, 0x86: 4, 0x87: 3, 0x95: 10)',
          '0x71 HitCounter of hero/T/T2/A/V/U: %s' % ' / '.join(str(R['self = %s (HitCounter in memory %d)' % (n, h)]) for n, h in (('hero', 2), ('T', 0), ('T2', 0), ('A', 1), ('V', 0), ('U', 0))),
          '0x86 value written (+0x1CC after): %s' % '; '.join('%s -> %s' % (lab.split(',')[0][:44], ' '.join('%s=%X' % (K.E_NAMES[s], v) for s, v in R[lab].items() if v)) for lab in R if isinstance(R[lab], dict)),
          '0x87 on T (hero sword, nibble 1), typeIdx 0..7: %s' % ' '.join(str(R['T (hero sword, nibble 1) typeIdx %d -> nibble %d' % (t, n)]) for t, n in ((0, 1), (1, 2), (2, 3), (3, 4), (4, 5), (5, 5), (6, 6), (7, 7))),
          '0x87 V (A, nibble 4) typeIdx 3 / 0: %s / %s; U (untouched) typeIdx 0 / 3: %s / %s; self (0x80) T typeIdx 0: %s' % (
              R['V (A, nibble 4) typeIdx 3 -> nibble 4'], R['V (A, nibble 4) typeIdx 0 -> nibble 1'], R['U (untouched) typeIdx 0'], R['U (untouched) typeIdx 3'], R['search 0x80 (self = T, the lever case) typeIdx 0']),
          '0x87 typeIdx 8..11 bytes %s: with A nibble 3 results %s; with A nibble 4 results %s' % (
              ' '.join('%02X' % b for b in k3.NIBBLE_BYTES[8:12]), ' '.join(str(R['V with A attack := synthetic attr 0x03 power 2 (nibble 3): typeIdx %d -> byte 0x%02X' % (t, k3.NIBBLE_BYTES[t])]) for t in (8, 9, 10, 11)),
              ' '.join(str(R['V (A, nibble 4) typeIdx %d -> byte 0x%02X' % (t, k3.NIBBLE_BYTES[t])]) for t in (8, 9, 10, 11))),
          '0x95 hero box: IN %s, typeIdx 3 %s, mask 0x40 %s, X right %s, X left %s, Y below %s, Y above %s, Z above %s' % (
              R['IN: rect (9,5,0) 2 x 2 units around the hero box, hero-only mask 0x0008, typeIdx 0 (sword)'], R['same rect, typeIdx 3 (nibble 4: no attacker of that kind there)'],
              R['same rect, mask 0x0040 (no entity has the bit)'], R['OUT in X, rectangle entirely right of the box: x unit 11'], R['OUT in X, rectangle entirely left of the box: x unit 0'],
              R['OUT in Y, rectangle below the box: y unit 7'], R['OUT in Y, rectangle above the box: y unit 0, 1 unit high'], R['OUT in Z, rectangle from z unit 3 (48 px): the box (z 0..15 px) is below it']),
          '0x95 zero mask: %s; typeIdx 8..11 with A nibble 3 and mask 4: %s' % (R['same rect, mask 0 (early exit at 0x80040818)'], ' '.join(str(R['rect around A, mask 0x0004, A attack nibble 3: typeIdx %d -> byte 0x%02X' % (t, k3.NIBBLE_BYTES[t])]) for t in (8, 9, 10, 11)))]
    return L


def main():
    rc = 0
    verdicts = []
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

    K.use_engine('emu')
    K.COVS[:] = []
    print('== 2. K1')
    sys.stdout.flush()
    l1, r1, ok1 = k1.run()
    w('K1.txt', '\n'.join(['# K1: equipment records and hero sheet, real UpdateItemEffectState 0x800307E8 (every value read from the emulated machine; see notes.md)', ''] + l1))
    dep = r1['dep']
    w('K1-dependency.txt', '\n'.join(['# every single-byte perturbation of the blob bytes of the records of inventory "all" (Holy Sword [1], leather armour [0], ring 0x33 [2]) that changed an output;',
                                      '# record k byte j xor mask -> outputs changed.  %d perturbations were run (record [0] bytes 0..254, records [1], [2] bytes 0..31; masks 0x01 0x40 0x80)' % dep['n_runs']] +
                                     ['R%d[%d] xor 0x%02X -> %s' % (k, j, mask, ','.join(ch)) for k, j, mask, ch in dep['changed']]))
    w('K1.json', jdump({k: v for k, v in r1.items() if k != 'dep'}))
    verdicts.append(('K1: records, sheet, boots equal to the independent model for %d inventories; dependency structure as described; %d+%d+%d inventory-side variants equal' % (len(K.INV), 99, 99, 7), ok1))
    print('   K1:', 'PASS' if ok1 else 'FAIL')
    sys.stdout.flush()

    print('== 3. K2')
    sys.stdout.flush()
    l2, r2, ok2 = k2.run()
    w('K2.txt', '\n'.join(['# K2: the real ComputeNewHp 0x8003A374 on a Beannoid (record 335, level D 0); see tools/k2.py for the states HERO, A10, NH, HT', ''] + l2))
    w('K2.json', jdump(r2))
    verdicts.append(('K2: ComputeNewHp equals the independent model on every line (HP after the call)', ok2))
    print('   K2:', 'PASS' if ok2 else 'FAIL')
    sys.stdout.flush()

    print('== 4. K3')
    sys.stdout.flush()
    l3, ok3, n3, m3 = k3.run()
    w('K3.txt', '\n'.join(['# K3: the real handlers 0x71, 0x86, 0x87, 0x95 (direct call and real program runner 0x8004205C); see tools/k3.py for the program state layout', ''] + l3))
    verdicts.append(('K3: %d checks, each: handler result == independent model == program-runner result, no unwritten-byte read' % n3, ok3))
    print('   K3:', 'PASS' if ok3 else 'FAIL', n3)
    sys.stdout.flush()
    k3res = dict(k3.RESULTS)
    emu_calls = dict(K.COUNT)
    covs = set()
    for c in K.COVS:
        covs |= c

    print('== 5. second interpreter')
    sys.stdout.flush()
    K.use_engine('mips')
    c0 = dict(K.COUNT)
    ll1, rr1, oo1 = k1.run()
    ll2, rr2, oo2 = k2.run()
    ll3, oo3, nn3, mm3 = k3.run()
    K.use_engine('emu')
    mips_calls = K.COUNT['mips'] - c0['mips']
    same1, same2, same3 = (ll1 == l1), (ll2 == l2), (ll3 == l3)
    d2 = ['# K1, K2, K3 run twice: once through emu.py (the oracle) and once through mips.py (a separately written interpreter, via interp2.py).',
          '# Every call of a function of the binary made by the K scripts (and every tick) goes through the interpreter under test; the whole output text is compared.',
          'K1 output text (%d lines): %s' % (len(l1), 'identical' if same1 else 'DIFFERENT'),
          'K2 output text (%d lines): %s' % (len(l2), 'identical' if same2 else 'DIFFERENT'),
          'K3 output text (%d lines): %s' % (len(l3), 'identical' if same3 else 'DIFFERENT'),
          'calls of binary functions made through emu.py: %d; through mips.py: %d' % (emu_calls['emu'], mips_calls)]
    if not same1:
        d2 += ['  K1 first difference: %s' % next(((a, b) for a, b in zip(l1, ll1) if a != b), None)]
    if not same2:
        d2 += ['  K2 first difference: %s' % next(((a, b) for a, b in zip(l2, ll2) if a != b), None)]
    if not same3:
        d2 += ['  K3 first difference: %s' % next(((a, b) for a, b in zip(l3, ll3) if a != b), None)]
    w('diff2.txt', '\n'.join(d2))
    d2ok = same1 and same2 and same3 and oo1 and oo2 and oo3
    verdicts.append(('two interpreters (emu.py, mips.py) give the identical text for K1, K2 and K3', d2ok))
    print('   diff2:', 'PASS' if d2ok else 'FAIL')
    sys.stdout.flush()

    print('== 6. mutation tests')
    sys.stdout.flush()
    ml, mok = kmut.run()
    w('mutations.txt', '\n'.join(ml))
    verdicts.append(('mutation tests: every mutation caught (predicate written beforehand + independent model disagrees), every control unchanged', mok))
    print('   mutations:', 'PASS' if mok else 'FAIL')
    sys.stdout.flush()

    print('== 7. coverage, load delay, disassembly')
    sys.stdout.flush()
    w('coverage.txt', '\n'.join(kcheck.coverage(covs)))
    ldl, lok = kcheck.load_delay(covs)
    w('load-delay-check.txt', '\n'.join(ldl))
    verdicts.append(('load-delay hazard check over every executed instruction', lok))
    dis = []
    for name, a, b in kcheck.RANGES:
        dis.append('## %s' % name)
        for ins in lib.dis(a, (b - a) // 4):
            dis.append('%08X  %08x  %-8s %s' % (ins.address, lib.u32(ins.address), ins.mnemonic, ins.op_str))
        dis.append('')
    w('dis.txt', '\n'.join(dis))
    head = ['# E20.c1b consequences-of-a-hit oracle: summary (every value computed by the emulated binary; see notes.md for the setup and the limits)', '', '## verdicts'] + \
           ['%s  %s' % ('PASS' if ok else 'FAIL', t) for t, ok in verdicts] + ['']
    summary = head + key_tables(r1, r2, k3res)
    w('summary.txt', '\n'.join(summary))
    names = []
    for root, dirs, files in os.walk(OUT):
        dirs.sort()
        for fn in sorted(files):
            p = os.path.join(root, fn)
            rel = os.path.relpath(p, OUT).replace('\\', '/')
            if rel != 'manifest.txt':
                names.append((rel, hashlib.md5(open(p, 'rb').read()).hexdigest()))
    names.sort()
    w('manifest.txt', '\n'.join('%s  %s' % (h, n) for n, h in names))
    print('== verdicts')
    for t, ok in verdicts:
        print('   %s  %s' % ('PASS' if ok else 'FAIL', t))
        rc |= (not ok)
    print('exit code', rc)
    return rc


if __name__ == '__main__':
    sys.exit(main())
