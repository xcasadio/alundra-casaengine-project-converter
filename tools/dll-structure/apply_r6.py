"""R6 (docs / research / converter comments): apply and check the inventory tools/dll-structure/r6-occurrences.tsv.

    python -B tools/dll-structure/apply_r6.py            # apply: replace every row exactly
    python -B tools/dll-structure/apply_r6.py apply      # same
    python -B tools/dll-structure/apply_r6.py --check    # closure check (exit 0 = clean)

Inventory format (r6-occurrences.tsv, UTF-8, LF, tab separated, header row):
    file   first   last   class   before   after   note
  - file: path relative to the repository root; first/last: 1-based line range of the change (inclusive);
  - class: A (path to a DLL source), B (type name in prose), C (naming rule), E (research script), F (stale statement);
  - before / after: the exact text of the lines first..last (without line terminators) before / after the change. Inside a
    cell, a newline is written \\n, a tab \\t and a backslash \\\\ so that the TSV stays one row per change;
  - note: free text (where the target comes from).

apply: every row is validated first against the current files (the lines first..last must equal "before"); one mismatch
  makes the tool exit 1 with nothing written. Rows are then applied bottom-up per file. A file keeps its line endings (CRLF
  stays CRLF, LF stays LF): the replaced lines take the terminator of the first replaced line.
--check: exit 0 only if (1) for each A, B, C, E, F row the "after" lines are present in the file and the "before" lines no
  longer are; (2) no tracked file differs from the base commit outside the paths of the slice; (3) plans, annexes, ADRs and
  the docs/*.txt traces are untouched.
"""
import csv, subprocess, sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
TSV = Path(__file__).resolve().parent / 'r6-occurrences.tsv'
BASE = '211a757'
EXTRA_ALLOWED = {'tools/dll-structure/r6-occurrences.tsv', 'tools/dll-structure/apply_r6.py', 'docs/plan-restructuration-dll.md'}


def dec(cell):
    out, i = [], 0
    while i < len(cell):
        c = cell[i]
        if c == '\\' and i + 1 < len(cell):
            n = cell[i + 1]
            out.append({'n': '\n', 't': '\t', '\\': '\\'}.get(n, '\\' + n))
            i += 2
        else:
            out.append(c)
            i += 1
    return ''.join(out).split('\n')


def load_rows():
    with open(TSV, encoding='utf-8', newline='') as fh:
        rows = list(csv.DictReader(fh, delimiter='\t', quoting=csv.QUOTE_NONE))
    for r in rows:
        r['first'], r['last'] = int(r['first']), int(r['last'])
        r['before'], r['after'] = dec(r['before']), dec(r['after'])
    return rows


def read_lines(rel):
    data = (ROOT / rel).read_bytes().decode('utf-8')
    return data.split('\n')          # a CR, when present, stays at the end of the line


def strip_cr(l):
    return l[:-1] if l.endswith('\r') else l


def contains_block(lines, block):
    plain = [strip_cr(l) for l in lines]
    n = len(block)
    return any(plain[i:i + n] == block for i in range(len(plain) - n + 1))


def apply():
    rows = load_rows()
    by_file = {}
    for r in rows:
        by_file.setdefault(r['file'], []).append(r)
    bad = []
    for f, rs in by_file.items():
        lines = read_lines(f)
        spans = sorted((r['first'], r['last']) for r in rs)
        for (a1, b1), (a2, b2) in zip(spans, spans[1:]):
            if a2 <= b1:
                bad.append(f'{f}: overlapping rows {a1}-{b1} and {a2}-{b2}')
        for r in rs:
            cur = [strip_cr(l) for l in lines[r['first'] - 1:r['last']]]
            if len(cur) != r['last'] - r['first'] + 1 or cur != r['before']:
                bad.append(f"{f}:{r['first']}-{r['last']}: current text differs from 'before'")
    if bad:
        print('REFUSED, nothing written:\n  ' + '\n  '.join(bad))
        return 1
    for f, rs in by_file.items():
        lines = read_lines(f)
        for r in sorted(rs, key=lambda r: r['first'], reverse=True):
            cr = '\r' if lines[r['first'] - 1].endswith('\r') else ''
            lines[r['first'] - 1:r['last']] = [t + cr for t in r['after']]
        (ROOT / f).write_bytes('\n'.join(lines).encode('utf-8'))
    print(f'applied {len(rows)} rows in {len(by_file)} files')
    return 0


def check():
    rows = [r for r in load_rows() if r['class'] in 'ABCEF']
    problems = []
    for r in rows:
        if not r['after'] or not any(r['after']):
            problems.append(f"{r['file']}:{r['first']}: empty 'after'")
            continue
        lines = read_lines(r['file'])
        if contains_block(lines, r['before']):
            problems.append(f"{r['file']}:{r['first']}-{r['last']}: old text still present")
        if not contains_block(lines, r['after']):
            problems.append(f"{r['file']}:{r['first']}-{r['last']}: new text missing")
    allowed = {r['file'] for r in load_rows()} | EXTRA_ALLOWED
    # committed range base..HEAD and the working tree against the base; submodules ignored (their local state is not ours)
    changed = subprocess.run(['git', '-C', str(ROOT), 'diff', '--name-only', '--ignore-submodules=all', BASE],
                             capture_output=True, text=True, encoding='utf-8').stdout.split()
    for f in changed:
        if f not in allowed:
            frozen = (f.startswith('docs/decisions/') or '-annexe/' in f or (f.startswith('docs/plan-') and f != 'docs/plan-restructuration-dll.md')
                      or (f.startswith('docs/') and f.endswith('.txt') and f.count('/') == 1))
            problems.append(f"{f}: changed since {BASE} outside the paths of the slice" + (' (frozen: plan, annex, ADR or trace)' if frozen else ''))
    if problems:
        print('CHECK FAILED:\n  ' + '\n  '.join(problems))
        return 1
    print(f'check ok: {len(rows)} rows (A, B, C, E, F) carry their new text and not the old one; {len(changed)} files changed since {BASE}, all in the slice')
    return 0


if __name__ == '__main__':
    mode = sys.argv[1] if len(sys.argv) > 1 else 'apply'
    sys.exit(check() if mode == '--check' else apply() if mode == 'apply' else 2)
