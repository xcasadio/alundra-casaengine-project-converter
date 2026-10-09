"""compare.py - D1 comparison step: the oracle's traces (out/S-A.trace.json, out/S-B.trace.json) against the annex values of
docs/plan-e19-d2c-valeurs.md section B.1 (blocks "## S-A" and "## S-B"), parsed from the text of the annex.
This is the ONLY place that reads the expected values.

Rule of the plan (docs/plan-e20-heros.md 1.2 D1): at each of the 24 compared ticks the oracle must give exactly the current animation, ForceX, ForceY, ForceZ,
PosX - start, PosY - start and IsOnGround of the annex, and the annex PosZ + 1 (the annex follows the DLL convention, the binary rests at terrain + 1).
CollidedWithEntityZ is compared too (reported separately).

usage: python -B compare.py [outdir]   -> writes outdir/comparison.txt, exit code 0 = PASS for both scenarios, 1 = a difference
"""
import json
import os
import re
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
ANNEX = os.path.join(HERE, '..', '..', '..', 'plan-e19-d2c-valeurs.md')


def parse_annex(path=ANNEX):
    text = open(path, encoding='utf-8').read().splitlines()
    blocks = {}
    cur = None
    for line in text:
        m = re.match(r'^## (S-[A-Z0-9]+) ', line)
        if m:
            cur = m.group(1)
            blocks[cur] = {}
            continue
        if cur and (line.startswith('```') or line.startswith('### ')):
            if line.startswith('```') and blocks[cur]:
                cur = None
            continue
        if cur:
            m = re.match(r'^([A-Za-z][A-Za-z0-9 ()+\-.,/]*?)\s*:\s*\[([-0-9, ]+)\]\s*$', line)
            if m:
                blocks[cur][m.group(1).strip()] = [int(x) for x in m.group(2).split(',')]
    return blocks


MAP = [   # (annex key, oracle field, transform of the oracle value given the start state)
    ('Current animation per tick', 'anim_cur', lambda v, s: v),
    ('ForceZ per tick', 'force_z', lambda v, s: v),
    ('PosZ (DLL, no +1) per tick', 'pos_z', lambda v, s: v - 1),          # oracle (binary convention) minus the +1 = the annex
    ('ForceX per tick', 'force_x', lambda v, s: v),
    ('ForceY per tick', 'force_y', lambda v, s: v),
    ('PosX - start (16.16)', 'pos_x', lambda v, s: v - s['pos_x']),
    ('PosY - start (16.16)', 'pos_y', lambda v, s: v - s['pos_y']),
    ('IsOnGround after tick', 'on_ground', lambda v, s: v),
    ('CollidedWithEntityZ', 'coll_z', lambda v, s: v),
]


def compare(name, outdir, annex):
    tr = json.load(open(os.path.join(outdir, name + '.trace.json')))
    return compare_rows(name, tr['rows'], tr['start'], annex)


def compare_rows(name, rows, start, annex):
    A = annex[name]
    lines = ['== %s: %d oracle ticks vs %d annex ticks' % (name, len(rows), len(A['Current animation per tick']))]
    first = None
    total = 0
    bad = 0
    per_field = {}
    for key, field, f in MAP:
        exp = A[key]
        got = [f(r[field], start) for r in rows]
        if len(exp) != len(got):
            lines.append('   LENGTH MISMATCH %s: annex %d, oracle %d' % (key, len(exp), len(got)))
            bad += 1
            continue
        diffs = [(i + 1, e, g) for i, (e, g) in enumerate(zip(exp, got)) if e != g]
        per_field[key] = len(diffs)
        total += len(exp)
        bad += len(diffs)
        for (t, e, g) in diffs:
            if first is None or t < first[0]:
                first = (t, key, e, g)
        lines.append('   %-30s %2d/%2d equal%s' % (key, len(exp) - len(diffs), len(exp), '' if not diffs else '   DIFF at ticks ' + str([d[0] for d in diffs])))
        for (t, e, g) in diffs[:6]:
            lines.append('        tick %d: annex %s, oracle %s' % (t, e, g))
    verdict = 'PASS' if bad == 0 else 'FAIL'
    lines.append('   VERDICT %s: %d values compared, %d differences%s' % (verdict, total, bad, '' if first is None else '; first: tick %d, %s, annex %s, oracle %s' % first))
    return verdict, lines


def detail(name, outdir, annex):
    """every compared value: annex value | oracle value (oracle PosZ already minus the +1), one line per tick"""
    tr = json.load(open(os.path.join(outdir, name + '.trace.json')))
    rows, start = tr['rows'], tr['start']
    A = annex[name]
    L = ['== %s: tick | ' % name + ' | '.join(k for k, _, _ in MAP)]
    for i, r in enumerate(rows):
        cells = []
        for key, field, f in MAP:
            e, g = A[key][i], f(r[field], start)
            cells.append('%d' % e if e == g else '%d != %d' % (e, g))
        L.append('%2d | ' % (i + 1) + ' | '.join(cells))
    L.append('(a cell shows the value once when the annex and the oracle are equal)')
    return L


def main(outdir):
    annex = parse_annex()
    allv = []
    out = ['# D1 comparison: oracle (emulated ALUN_CD.EXE) vs docs/plan-e19-d2c-valeurs.md B.1, field by field',
           '# the annex PosZ is in the DLL convention; the oracle PosZ minus 1 is compared (plan: annex + 1 = binary)']
    for name in ('S-A', 'S-B'):
        v, lines = compare(name, outdir, annex)
        allv.append(v)
        out += lines
    out.append('OVERALL: ' + ('PASS' if all(v == 'PASS' for v in allv) else 'FAIL'))
    open(os.path.join(outdir, 'comparison.txt'), 'w', encoding='utf-8').write('\n'.join(out) + '\n')
    dl = []
    for name in ('S-A', 'S-B'):
        dl += detail(name, outdir, annex) + ['']
    open(os.path.join(outdir, 'comparison-detail.txt'), 'w', encoding='utf-8').write('\n'.join(dl) + '\n')
    print('\n'.join(out))
    return 0 if all(v == 'PASS' for v in allv) else 1


if __name__ == '__main__':
    sys.exit(main(sys.argv[1] if len(sys.argv) > 1 else os.path.join(HERE, '..', 'out')))
