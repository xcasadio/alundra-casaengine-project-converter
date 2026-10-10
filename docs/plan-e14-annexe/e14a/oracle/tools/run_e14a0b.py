"""run_e14a0b.py - E14.a0b T1: everything, in order.   usage: python -B tools/run_e14a0b.py     (exit code 0 = every check below passed)

 1. h1  the shared helpers (relpos, relpos2, TryAttackPlayer2, TryAttackPlayerFront, HandleAnimationDirection, StartWalk, the direction routine, D1, D9) on 10,000 random states each: emulated binary == independent model,
        second interpreter (mips.py via interp2.py) identical text, write-set and unwritten-byte checks, instruction coverage.  Outputs: out/h1/<helper>.txt.gz (inputs and outputs of every state).
 2. h2  the floor and the four corner heights of the Beannoid / Muruta bodies and of the hero on the real cells of maps 14, 115, 116.  Outputs: out/h2/map_<N>.txt.gz.
 3. mutation tests and controls (tools/hmut.py).
 4. load-delay check over every executed instruction, disassembly of the functions under test, summary, manifest.
Output is byte-stable (no clock, no host path, no hash-ordered iteration); the manifest holds the md5 of the DECOMPRESSED text of the .gz files.
"""
import gzip
import hashlib
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
OUT = os.path.normpath(os.path.join(HERE, '..', 'out'))
os.makedirs(os.path.join(OUT, 'h1'), exist_ok=True)
os.makedirs(os.path.join(OUT, 'h2'), exist_ok=True)
import h1
import h2
import hmut
import kcheck
import lib


def w(name, text):
    with open(os.path.join(OUT, name), 'w', encoding='utf-8', newline='\n') as f:
        f.write(text if text.endswith('\n') else text + '\n')


def per_record_table(texts):
    """creation rows (variant 0, the first hero place of each body): the terrain, floor and corners the real spawner left, in pixels"""
    L = ['# the values the real spawner left (variant 0, hero 0,0): body record, body terrain height (px) and floor - terrain / corners in raw units; hero terrain (px)']
    for n in sorted(texts):
        seen = set()
        for line in texts[n].splitlines():
            if line.startswith('#'):
                continue
            head, e, hh, r = [x.split() for x in line.split('|')]
            head = [int(x) for x in head]
            e = [int(x) for x in e]
            hh = [int(x) for x in hh]
            if head[2] == 0 and head[3] == 0 and head[4] == 0 and head[1] not in seen:
                seen.add(head[1])
                L.append('map %-3d record %-3d at (%d, %d) px, PosZ raw %d: terrain %g px, floor %d (= terrain + 1), corners %s; hero beside: terrain %g px' % (
                    n, head[1], e[0] >> 16, e[1] >> 16, e[2], e[11] / 65536.0, e[12], e[13:17], hh[11] / 65536.0))
    return L


def formula_table(texts):
    """the differences the DLL must reproduce (floor - corner[k], |dz|): counted on the binary's rows"""
    L = ['# per map: rows, rows on flat ground (body terrain 0) and raised ground, and the values the C# ports must reproduce: floor - terrain (body / hero), floor - corner[k] distinct values, |dz| range']
    for n in sorted(texts):
        rows = []
        for line in texts[n].splitlines():
            if line.startswith('#'):
                continue
            head, e, hh, r = [x.split() for x in line.split('|')]
            rows.append(([int(x) for x in head], [int(x) for x in e], [int(x) for x in hh], [int(x) for x in r]))
        flat = sum(1 for _, e, _, _ in rows if e[11] == 0)
        raised = len(rows) - flat
        fe = sorted({e[12] - e[11] for _, e, _, _ in rows})
        fh = sorted({h[12] - h[11] for _, _, h, _ in rows})
        diffs = sorted({e[12] - e[13 + k] for _, e, _, _ in rows for k in range(4)})
        dz = [r[2] for _, _, _, r in rows]
        L.append('map %-3d %4d rows: body on flat ground %d, raised %d; floor - terrain body %s hero %s; body floor - corner[k] takes %d distinct values (min %d, max %d, in raw units, 1 on flat ground); |dz| from %d to %d' % (
            n, len(rows), flat, raised, fe, fh, len(diffs), diffs[0], diffs[-1], min(dz), max(dz)))
    return L


def main():
    verdicts = []
    covs = set()
    print('== 1. h1: the helpers, %d states each' % h1.N_CASES)
    sys.stdout.flush()
    l1, ok1, t1, c1 = h1.run_all(None, h1.N_CASES)
    covs |= c1
    for name, text in t1.items():
        raw = h1.write_gz(os.path.join(OUT, 'h1', name + '.txt.gz'), text)
    w('h1.txt', '\n'.join(['# h1: each helper run as real code of ALUN_CD.EXE (France) on %d random states (generator tools/hmodel.py Rng, seeds in tools/h1.py); the inputs and outputs of every state are in out/h1/<helper>.txt.gz' % h1.N_CASES] + l1))
    verdicts.append(('h1: %d helpers x %d states: emulated binary == independent model, second interpreter identical, write sets and unwritten-byte reads clean, instruction coverage complete' % (len(l1), h1.N_CASES), ok1))
    print('   h1:', 'PASS' if ok1 else 'FAIL')
    sys.stdout.flush()

    print('== 2. h2: floor and corners on the real cells of maps 14, 115, 116')
    sys.stdout.flush()
    l2, ok2, t2, c2 = h2.run_all()
    covs |= c2
    for n, text in t2.items():
        h1.write_gz(os.path.join(OUT, 'h2', 'map_%d.txt.gz' % n), text)
    w('h2.txt', '\n'.join(['# h2: ground of the bodies and of the hero on the real cells (rows in out/h2/map_<N>.txt.gz; layout in tools/h2.py)'] + l2 + [''] + formula_table(t2) + [''] + per_record_table(t2)))
    verdicts.append(('h2: terrain, four corners and floor of every body and hero row == independent model (terrain = max(0, corners), floor = terrain + 1); second interpreter identical', ok2))
    print('   h2:', 'PASS' if ok2 else 'FAIL')
    sys.stdout.flush()

    print('== 3. mutation tests')
    sys.stdout.flush()
    ml, mok = hmut.run()
    w('mutations.txt', '\n'.join(ml))
    verdicts.append(('mutation tests: every mutation caught (predicate written beforehand + independent model disagrees), every control unchanged', mok))
    print('   mutations:', 'PASS' if mok else 'FAIL')
    sys.stdout.flush()

    print('== 4. load delay, disassembly, summary, manifest')
    sys.stdout.flush()
    ldl, lok = kcheck.load_delay(covs)
    w('load-delay-check.txt', '\n'.join(ldl))
    verdicts.append(('load-delay hazard check over every executed instruction', lok))
    ranges = [(name, a, b) for name, a, b in h1.RANGES.values()] + [
        ('ComputeEntityGroundHeight 0x800370C4', 0x800370C4, 0x80037364), ('GetCollisionOnZ (floor) 0x80037F28', 0x80037F28, 0x80038064), ('UpdateEntityTileState 0x80038064', 0x80038064, 0x80038364),
        ('SetEntityDimensions 0x80039C40', 0x80039C40, 0x80039D04)]
    dis = []
    for name, a, b in ranges:
        dis.append('## %s' % name)
        for ins in lib.dis(a, (b - a) // 4):
            dis.append('%08X  %08x  %-8s %s' % (ins.address, lib.u32(ins.address), ins.mnemonic, ins.op_str))
        dis.append('')
    w('dis.txt', '\n'.join(dis))
    head = ['# E14.a0b T1 oracle: summary (every value computed by the emulated binary; see notes.md for the expected values written before the runs and for the limits)', '', '## verdicts'] + \
           ['%s  %s' % ('PASS' if ok else 'FAIL', t) for t, ok in verdicts] + ['', '## h1'] + l1 + ['', '## h2'] + l2 + ['', '## key values for the ports'] + formula_table(t2) + ['', '## mutations'] + [x for x in ml if x[:6] in ('CAUGHT', 'OK    ', 'MISSED', 'FAILED')]
    w('summary.txt', '\n'.join(head))
    names = []
    for root, dirs, files in os.walk(OUT):
        dirs.sort()
        for fn in sorted(files):
            p = os.path.join(root, fn)
            rel = os.path.relpath(p, OUT).replace('\\', '/')
            if rel == 'manifest.txt':
                continue
            data = open(p, 'rb').read()
            if fn.endswith('.gz'):
                data = gzip.decompress(data)
            names.append((rel + (' (decompressed)' if fn.endswith('.gz') else ''), hashlib.md5(data).hexdigest()))
    names.sort()
    w('manifest.txt', '\n'.join('%s  %s' % (h, n) for n, h in names))
    rc = 0
    print('== verdicts')
    for t, ok in verdicts:
        print('   %s  %s' % ('PASS' if ok else 'FAIL', t))
        rc |= (not ok)
    print('exit code', rc)
    return rc


if __name__ == '__main__':
    sys.exit(main())
