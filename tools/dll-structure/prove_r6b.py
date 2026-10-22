"""R6b proof: in the 21 living documents only the numbers of the cited ranges changed, and every range designates at the pinned trees what its
sentence names.

    python -B tools/dll-structure/prove_r6b.py            prove the working tree (the 21 documents) against r6b-citations.tsv
    python -B tools/dll-structure/prove_r6b.py --files    also: the commits since the base touch only the documents, tools/dll-structure/ and the plan,
                                                          and no submodule pointer moved
    python -B tools/dll-structure/prove_r6b.py --selftest the mutations that the proof must refuse, and the states it must accept

What is checked
  A  the survey covers EVERY range of the documents at the base (33528d55), in order, with the same text and column, plus the restored citation;
  B  each document of the working tree is the document of the base with the edits of the survey and NOTHING else (every other byte, the line
     endings included: CRLF everywhere, no lone LF);
  C  the ranges read in the working tree are the `after` column, in order (no stray citation, none missing);
  D  for each range, read at the pinned trees (decompilation and engine at the pointers of the base commit, git show; the parent at the base):
     the lines of start and of end exist and their text equals the probes of the survey; a `whole` range starts at the declaration of the
     first member it names and ends at the closing brace of the last or at its last statement; a `part` starts at the declaration and ends
     inside the member on a line of code; a `decl` is the declaration line; any other range starts and ends on a line of code of the block.
Exit 0 = proved, 1 = refused (every finding listed).
"""
import argparse
import re
import subprocess
import sys

import apply_r6b as A
import r6b_lib as L

ALLOWED_FILES = set(L.DOCS) | {'docs/plan-restructuration-dll.md'}
ALLOWED_PREFIX = ('tools/dll-structure/',)


def is_text(line):
    s = line.strip()
    return bool(s) and not s.startswith('//')


# ------------------------------------------------------------------------------------------------------------------ A, B, C
def check_coverage(rows):
    out = []
    base = [c for c in L.extract_all()]
    keyed = {(r['doc'], int(r['line']), int(r['k'])): r for r in rows if r['before'] != ''}
    if len(keyed) != sum(1 for r in rows if r['before'] != ''):
        out.append('A: duplicated rows in the survey')
    for c in base:
        r = keyed.get((c['doc'], c['line'], c['k']))
        if r is None:
            out.append(f'A: {c["doc"]}:{c["line"]} range {c["k"]} ({c["before"]}) is not in the survey')
        elif r['before'] != c['before'] or int(r['col']) != c['col']:
            out.append(f'A: {c["doc"]}:{c["line"]} range {c["k"]}: the survey says {r["before"]}@{r["col"]}, the base has {c["before"]}@{c["col"]}')
    bk = {(c['doc'], c['line'], c['k']) for c in base}
    for k in keyed:
        if k not in bk:
            out.append(f'A: the survey has {k}, the base has no such range')
    rest = [r for r in rows if r['before'] == '']
    if len(rest) != 1 or (rest[0]['doc'], int(rest[0]['line'])) != (A.R, 227):
        out.append('A: the survey must hold exactly one restored citation, at docs/intro-roadmap.md:227')
    return out


def expected_texts(rows):
    by = {}
    for r in rows:
        by.setdefault(r['doc'], {}).setdefault(int(r['line']), []).append(r)
    out = {}
    for doc in L.DOCS:
        lines = L.text_at(L.BASE_COMMIT, doc).split('\n')
        for ln, rs in by.get(doc, {}).items():
            lines[ln - 1] = A.expected_line(lines[ln - 1], rs)
        out[doc] = '\n'.join(lines)
    return out


def check_texts(texts, rows):
    out = []
    try:
        exp = expected_texts(rows)
    except Exception as e:
        return [f'B: the survey cannot be applied to the base ({e})']
    for doc in L.DOCS:
        got = texts.get(doc)
        if got is None:
            out.append(f'B: {doc} is missing')
            continue
        if got != exp[doc]:
            gl, el = got.split('\n'), exp[doc].split('\n')
            if len(gl) != len(el):
                out.append(f'B: {doc}: {len(gl)} lines, expected {len(el)}')
            for i, (g, e) in enumerate(zip(gl, el), 1):
                if g != e:
                    out.append(f'B: {doc}:{i} differs from the base plus the survey\n      got      {g[:160]}\n      expected {e[:160]}')
    return out


def check_eol(raws, rows):
    """One style of line ending per document; the documents that hold changed ranges were CRLF at the base and stay so."""
    out = []
    changed = {r['doc'] for r in rows if r['class'] != 'juste'}
    for doc, raw in raws.items():
        lone = len(re.findall(rb'(?<!\r)\n', raw))
        crlf = raw.count(b'\r\n')
        if lone and crlf:
            out.append(f'B: {doc}: mixed line endings ({crlf} CRLF, {lone} lone LF)')
        elif lone and doc in changed:
            out.append(f'B: {doc}: {lone} lone LF (this document is CRLF in the working tree)')
    return out


def ranges_of(texts):
    """The ranges read in the documents: (doc, line, text of the range) in order, from the same reader as the survey but on the given texts."""
    saved = L.text_at
    try:
        L.text_at = lambda commit, path: texts[path]
        return [(c['doc'], c['line'], c['before']) for c in L.extract_all('working')]
    finally:
        L.text_at = saved


def check_after(texts, rows):
    out = []
    want = sorted(((r['doc'], int(r['line']), int(r['col']), r['after']) for r in rows if r['before'] != ''), key=lambda x: (L.DOCS.index(x[0]), x[1], x[2]))
    want += [(r['doc'], int(r['line']), int(r['col']), r['after']) for r in rows if r['before'] == '']
    want_by_line = {}
    for d, ln, col, a in sorted(want, key=lambda x: (L.DOCS.index(x[0]), x[1], x[2])):
        want_by_line.setdefault((d, ln), []).append(a)
    got_by_line = {}
    for d, ln, t in ranges_of(texts):
        got_by_line.setdefault((d, ln), []).append(t)
    for key in sorted(set(want_by_line) | set(got_by_line), key=lambda x: (L.DOCS.index(x[0]), x[1])):
        if want_by_line.get(key) != got_by_line.get(key):
            out.append(f'C: {key[0]}:{key[1]}: ranges read {got_by_line.get(key)}, the survey says {want_by_line.get(key)}')
    return out


# ------------------------------------------------------------------------------------------------------------------ D
def check_trees(rows):
    out = []
    for r in rows:
        where = f'{r["doc"]}:{r["line"]} [{r["cited"]}:{r["after"]}]'
        lines = L.target_lines(r['target'])
        if lines is None:
            out.append(f'D: {where}: target {r["target"]} not readable')
            continue
        lo, hi = (int(x) for x in r['after'].split('-')) if '-' in r['after'] else (int(r['after']),) * 2
        if lo < 1 or hi > len(lines) or lo > hi:
            out.append(f'D: {where}: the range is outside {r["target"]} ({len(lines)} lines)')
            continue
        if L.probe(lines, lo) != r['probe_start']:
            out.append(f'D: {where}: line {lo} reads {L.probe(lines, lo)!r}, the survey says {r["probe_start"]!r}')
        if L.probe(lines, hi) != r['probe_end']:
            out.append(f'D: {where}: line {hi} reads {L.probe(lines, hi)!r}, the survey says {r["probe_end"]!r}')
        scope, members = r['scope'], r['members']
        text_target = r['target'].endswith('.md')
        if scope in ('whole', 'part', 'decl'):
            names = members.split('+')
            first = L.member_span(lines, names[0], lo)
            last = L.member_span(lines, names[-1], hi)
            if first is None or last is None:
                out.append(f'D: {where}: member {members} not found in {r["target"]}')
                continue
            if first[0] != lo:
                out.append(f'D: {where}: {names[0]} is declared at {first[0]}, the range starts at {lo}')
            if scope == 'decl' and hi != lo:
                out.append(f'D: {where}: a decl range is one line')
            if scope == 'whole' and hi not in (last[1], last[1] - 1):
                out.append(f'D: {where}: {names[-1]} closes at {last[1]}, the range ends at {hi}')
            if scope == 'part' and not (lo <= hi <= first[1] and is_text(lines[hi - 1])):
                out.append(f'D: {where}: the range does not end on a line of code inside {names[0]} (closes at {first[1]})')
        elif scope == '-':
            for n in {lo, hi}:
                if not is_text(lines[n - 1]):
                    out.append(f'D: {where}: line {n} is blank or a comment ({lines[n - 1].strip()[:60]!r})')
        elif scope == 'tracked':
            pass
        else:
            out.append(f'D: {where}: unknown scope {scope!r}')
    return out


def prove(texts, rows):
    return check_coverage(rows) + check_texts(texts, rows) + check_after(texts, rows) + check_trees(rows)


# ------------------------------------------------------------------------------------------------------------------ the tree
def working_raws():
    return {d: (L.ROOT / d).read_bytes() for d in L.DOCS}


def working_texts(raws):
    return {d: b.decode('utf-8').replace('\r\n', '\n') for d, b in raws.items()}


def check_files():
    out = []
    r = subprocess.run(['git', '-C', str(L.ROOT), 'diff', '--name-only', L.BASE_COMMIT, 'HEAD'], capture_output=True, text=True)
    for p in r.stdout.split('\n'):
        if p and p not in ALLOWED_FILES and not p.startswith(ALLOWED_PREFIX):
            out.append(f'F: {p} changed since {L.BASE_COMMIT}, outside the documents, tools/dll-structure/ and the plan')
    r = subprocess.run(['git', '-C', str(L.ROOT), 'diff', L.BASE_COMMIT, 'HEAD', '--', 'CasaEngineMonogame', 'alundra-datas-analyser'], capture_output=True, text=True)
    if r.stdout.strip():
        out.append('F: a submodule pointer moved')
    return out


# ------------------------------------------------------------------------------------------------------------------ the self-test
def selftest(rows):
    exp = expected_texts(rows)
    bad = 0

    def case(name, texts, rws, expect_ok):
        nonlocal bad
        f = prove(texts, rws)
        ok = (not f) == expect_ok
        if not ok:
            bad += 1
        print(f'{"ok  " if ok else "FAIL"} {name}: {"accepted" if not f else "refused (" + str(len(f)) + " findings, first: " + f[0].splitlines()[0][:100] + ")"}')

    def tweak(doc, ln, fn):
        t = dict(exp)
        lines = t[doc].split('\n')
        lines[ln - 1] = fn(lines[ln - 1])
        t[doc] = '\n'.join(lines)
        return t

    case('the arrival (base plus the survey)', exp, rows, True)
    base = {d: L.text_at(L.BASE_COMMIT, d) for d in L.DOCS}
    case('the departure (survey not applied)', base, rows, False)
    case('a number changed outside any citation', tweak(A.R, 1, lambda s: s + ' 2026'), rows, False)
    case('a word changed on a citation line', tweak(A.R, 59, lambda s: s.replace('primitive', 'primitiv')), rows, False)
    case('a range changed in the document only', tweak(A.R, 197, lambda s: s.replace(':1518', ':1519')), rows, False)
    case('a stray citation added', tweak(A.R, 40, lambda s: s + ' (`GameEngine.cs:12`)'), rows, False)
    case('a citation removed', tweak(A.R, 336, lambda s: s.replace('GameEngine.cs:1518', 'GameEngine.cs')), rows, False)
    case('a last digit changed', tweak(A.C, 103, lambda s: s.replace('287-358', '287-359')), rows, False)

    def mut(fn):
        rs = [dict(r) for r in rows]
        fn(rs)
        return rs

    def row(rs, doc, line, k=0):
        return next(r for r in rs if r['doc'] == doc and int(r['line']) == line and int(r['k']) == k)

    # the survey and the document edited together: only the trees can refuse
    def shift(rs):
        r = row(rs, A.R, 197)
        r['after'] = '1519'
    rs = mut(shift)
    case('a range on a blank line (document and survey agree)', expected_with(rs), rs, False)

    def probe_off(rs):
        row(rs, A.R, 61)['probe_start'] = 'public int Script_139_08C('
    case('a probe that does not read', exp, mut(probe_off), False)

    def wrong_start(rs):
        r = row(rs, A.R, 60)
        r['after'] = '1113-1129'
        r['probe_start'] = L.probe(L.target_lines(r['target']), 1113)
    rs = mut(wrong_start)
    case('a whole range that does not start at the declaration', expected_with(rs), rs, False)

    def wrong_end(rs):
        r = row(rs, A.R, 60)
        r['after'] = '1112-1126'
        r['probe_end'] = L.probe(L.target_lines(r['target']), 1126)
    rs = mut(wrong_end)
    case('a whole range that stops before the end of the member', expected_with(rs), rs, False)

    def past_end(rs):
        r = row(rs, A.R, 196)
        r['after'] = '1500-1592'
        r['probe_end'] = L.probe(L.target_lines(r['target']), 1592)
    rs = mut(past_end)
    case('a range that runs into the next member', expected_with(rs), rs, False)
    case('a row missing from the survey', exp, [r for r in rows if not (r['doc'] == A.R and int(r['line']) == 288)], False)

    def wrong_col(rs):
        row(rs, A.R, 288)['col'] = int(row(rs, A.R, 288)['col']) + 1
    case('a column that is not the base column', exp, mut(wrong_col), False)
    eol_ok = not check_eol({d: t.replace('\n', '\r\n').encode('utf-8') for d, t in exp.items()}, rows)
    eol_bad = bool(check_eol({**{d: t.replace('\n', '\r\n').encode('utf-8') for d, t in exp.items()}, A.R: exp[A.R].encode('utf-8')}, rows))
    for name, ok in (('CRLF everywhere', eol_ok), ('one document in LF', eol_bad)):
        if not ok:
            bad += 1
        print(f'{"ok  " if ok else "FAIL"} line endings: {name}')
    print('selftest:', 'all the verdicts are the expected ones' if not bad else f'{bad} unexpected verdict(s)')
    return bad == 0


def expected_with(rows):
    return expected_texts(rows)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('--files', action='store_true')
    ap.add_argument('--selftest', action='store_true')
    a = ap.parse_args()
    rows = A.read_tsv()
    if a.selftest:
        sys.exit(0 if selftest(rows) else 1)
    raws = working_raws()
    findings = check_eol(raws, rows) + prove(working_texts(raws), rows)
    if a.files:
        findings += check_files()
    for f in findings:
        print(f)
    from collections import Counter
    print(f'{len(rows)} ranges ({dict(Counter(r["class"] for r in rows))}); {"REFUSED: " + str(len(findings)) + " findings" if findings else "proved"}')
    sys.exit(1 if findings else 0)


if __name__ == '__main__':
    main()
