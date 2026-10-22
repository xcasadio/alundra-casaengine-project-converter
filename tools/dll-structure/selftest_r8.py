"""R8 (P-R8-6): the mutations that `prove_r8.py` must refuse, and the states it must accept.

    python -B tools/dll-structure/selftest_r8.py <work folder outside the repository> [--base REV]

Builds the tree of departure (the `.cs` of `Alundra/` and `Alundra.Tests/` and the two other files, as the blobs of REV, default 55fac9af) in
<work>/A, copies it to <work>/B and runs `apply_r8.py` on B (the `.cs`, then `--other`).  Then:
  accepted: the departure -> the arrival (the `.cs`, all scopes; the DLL alone with --scope dll; the tests alone with --scope tests; --other);
  refused: departure -> departure (survey not applied); a code identifier, a string literal, a comment outside the survey, an omitted entry,
           a name of the decompilation, the word `Alundra` alone, a substring substitution (AlundraMusicPlayerTests -> BgmPlayerTests), a line
           added, a BOM removed, line endings changed, a kept name modified (two of them), a file changed outside the scope, a file removed;
           for --other: another JSON key, the `_comment` with a substring substitution, the XML changed outside its comment, the departure.
The comparisons are made on dictionaries in memory (path -> bytes), so nothing but A and B is written.  Exit 0 when every case gives the
expected verdict.
"""
import os
import re
import subprocess
import sys

import r8_lib as L
import prove_r8 as P

BASE = '55fac9af'


def build(work, rev):
    paths = L.list_cs_git(rev) + list(L.OTHER_FILES)
    blobs = L.blobs_at(rev, paths)
    for side in ('A', 'B'):
        for p, data in blobs.items():
            dst = os.path.join(work, side, p)
            os.makedirs(os.path.dirname(dst), exist_ok=True)
            with open(dst, 'wb') as f:
                f.write(data)
    for args in (['--scope', 'all'], ['--other']):
        r = subprocess.run([sys.executable, '-B', L.HERE + '/apply_r8.py', '--repo', os.path.join(work, 'B')] + args, capture_output=True, text=True)
        print('apply', args, '->', r.stdout.strip() or r.stderr.strip())
        if r.returncode != 0:
            raise SystemExit('apply failed')


def comment_spans(body):
    src = L.A.Src(body)
    return [(a, b) for a, b, k in src.spans if k == 'c'], src


def edit(d, path, fn):
    """A copy of the dictionary d with fn(bom, body) -> (bom, body) applied to one file."""
    out = dict(d)
    bom, body = L.decode(out[path])
    bom, body = fn(bom, body)
    out[path] = L.encode(bom, body)
    return out


def run_case(name, before, after, expect, scope='all', other=False, why=None):
    stats = {'files': 0, 'events': 0, 'lines': 0}
    errs = P.prove(before, after, scope, other, stats)
    got = 'accepted' if not errs else 'refused'
    ok = got == expect and (any(why in e for e in errs) if why else not any('outside the scope' in e for e in errs))
    print('%s %-62s %s%s' % ('ok  ' if ok else 'FAIL', name, got, '' if not errs else '  (%s)' % errs[0].split('\n')[0][:90]))
    return ok


def main():
    if len(sys.argv) < 2:
        raise SystemExit(__doc__)
    work = os.path.abspath(sys.argv[1])
    rev = sys.argv[sys.argv.index('--base') + 1] if '--base' in sys.argv else BASE
    if os.path.abspath(L.REPO) in work:
        raise SystemExit('the work folder must be outside the repository')
    build(work, rev)
    A, B = P.read_dir(os.path.join(work, 'A')), P.read_dir(os.path.join(work, 'B'))
    surv = L.load_survey()
    Bcs = {p: (A[p] if p in L.OTHER_FILES else B[p]) for p in A}
    Bot = {p: (B[p] if p in L.OTHER_FILES else A[p]) for p in A}
    res = []

    # --- accepted
    res.append(run_case('departure -> arrival (.cs, all)', A, Bcs, 'accepted'))
    dll_only = {p: (Bcs[p] if p.startswith('Alundra/') and p.endswith('.cs') else A[p]) for p in A}
    tests_only = {p: (Bcs[p] if p.startswith('Alundra.Tests/') and p.endswith('.cs') else A[p]) for p in A}
    res.append(run_case('departure -> DLL only, --scope dll', A, dll_only, 'accepted', scope='dll'))
    res.append(run_case('departure -> tests only, --scope tests', A, tests_only, 'accepted', scope='tests'))
    res.append(run_case('departure -> DLL only, --scope all (tests not applied)', A, dll_only, 'refused'))
    res.append(run_case('departure -> arrival (--other)', A, Bot, 'accepted', other=True))
    # --- refused
    res.append(run_case('departure -> departure (survey not applied)', A, A, 'refused'))

    # a code identifier
    f = 'Alundra/Hero/HeroController.cs'
    def m_code(bom, body):
        src = L.A.Src(body)
        i = src.masked.index('namespace')
        return bom, body[:i] + 'namespacf' + body[i + 9:]
    res.append(run_case('a code identifier changed', A, edit(Bcs, f, m_code), 'refused'))

    # a string literal
    def m_lit(bom, body):
        src = L.A.Src(body)
        for a, b, k in src.spans:
            if k == 's' and b - a > 6 and body[a] == '"' and body[a + 3].isalpha():
                return bom, body[:a + 3] + ('Q' if body[a + 3] != 'Q' else 'R') + body[a + 4:]
        raise SystemExit('no literal')
    res.append(run_case('a string literal changed', A, edit(Bcs, f, m_lit), 'refused'))

    # a comment outside the survey
    def m_com(bom, body):
        spans, _ = comment_spans(body)
        for a, b in spans:
            if body[a:a + 2] == '//' and b - a > 10:
                return bom, body[:a + 5] + ('Z' if body[a + 5] != 'Z' else 'Y') + body[a + 6:]
        raise SystemExit('no comment')
    res.append(run_case('a comment outside the survey changed', A, edit(Bcs, f, m_com), 'refused'))

    # an omitted entry: the first auto row of a file is not applied
    fa = sorted(p for p in surv if p.endswith('.cs') and any(r['class'] == 'auto' for r in surv[p].values()))[0]
    def m_omit(bom, body):
        bom_a, body_a = L.decode(A[fa])
        cands = L.cs_candidates(fa, body_a)
        rows = {k: dict(r) for k, r in surv[fa].items()}
        first = next(c for c in cands if rows[(c.line, c.rank)]['class'] == 'auto')
        rows[(first.line, first.rank)]['class'] = 'kept'
        nb, _ = L.rewrite(body_a, cands, rows)
        return bom_a, nb
    res.append(run_case('an entry of the survey omitted', A, edit(Bcs, fa, m_omit), 'refused'))

    # a name of the decompilation
    def m_disp(bom, body):
        return bom, body.replace('AlundraDisplay.cs', 'Display.cs', 1)
    res.append(run_case('AlundraDisplay.cs (decompilation) rewritten', A, edit(Bcs, 'Alundra/UI/Choice/ChoiceScreen.cs', m_disp), 'refused'))

    # the word Alundra alone
    def m_word(bom, body):
        spans, _ = comment_spans(body)
        for a, b in spans:
            m = re.search(r'(?<![\w/.\\])Alundra(?![\w/.\\])', body[a:b])
            if m:
                s = a + m.start()
                return bom, body[:s] + 'Gamer' + body[s + 7:]
        raise SystemExit('no word')
    fw = next(p for p in sorted(B) if p.endswith('.cs') and re.search(r'//[^\n]*(?<![\w/.\\])Alundra(?![\w/.\\])', L.decode(B[p])[1]))
    res.append(run_case('the word Alundra alone rewritten in a comment', A, edit(Bcs, fw, m_word), 'refused'))

    # a substring substitution
    fs = next(p for p in sorted(surv) if p.endswith('.cs') and any(r['old'] == 'AlundraMusicPlayerTests' for r in surv[p].values()))
    def m_sub(bom, body):
        i = body.index('MusicPlayerTests')
        return bom, body[:i] + 'BgmPlayerTests' + body[i + len('MusicPlayerTests'):]
    res.append(run_case('AlundraMusicPlayerTests -> BgmPlayerTests (substring)', A, edit(Bcs, fs, m_sub), 'refused'))

    # a line added
    res.append(run_case('a line added', A, edit(Bcs, f, lambda bom, body: (bom, body.replace('\n', '\n\n', 1))), 'refused'))

    # a BOM removed (a file that has one)
    fb = next(p for p in sorted(B) if p.endswith('.cs') and L.decode(B[p])[0] and p in surv)
    res.append(run_case('a BOM removed', A, edit(Bcs, fb, lambda bom, body: ('', body)), 'refused'))

    # line endings changed
    res.append(run_case('line endings changed (LF -> CRLF)', A, edit(Bcs, fa, lambda bom, body: (bom, body.replace('\n', '\r\n'))), 'refused'))

    # a kept name modified (two of them)
    res.append(run_case('a kept name modified: AlundraGame.json', A,
                        edit(Bcs, 'Alundra.Tests/Dialogues/DialogueScreenAssetTests.cs', lambda bom, body: (bom, body.replace('AlundraGame.json', 'Game.json', 1))), 'refused'))
    res.append(run_case('a kept name modified: Alundra/Scripts/PlayerManager.cs', A,
                        edit(Bcs, 'Alundra/Hero/HeroController.cs', lambda bom, body: (bom, body.replace('Alundra/Scripts/PlayerManager.cs', 'Alundra/Hero/PlayerManager.cs', 1))), 'refused'))

    # a file changed outside the scope; a file removed
    res.append(run_case('a test file changed, --scope dll', A, tests_only, 'refused', scope='dll', why='outside the scope'))
    gone = dict(Bcs)
    del gone[fa]
    res.append(run_case('a file removed', A, gone, 'refused'))

    # --- the two other files
    jf, cf = L.OTHER_FILES
    def j_key(bom, body):
        return bom, body.replace('"parallelizeTestCollections": false', '"parallelizeTestCollections": true')
    res.append(run_case('--other: a JSON value other than _comment changed', A, edit(Bot, jf, j_key), 'refused', other=True))
    def j_key2(bom, body):
        return bom, body.replace('"parallelizeTestCollections"', '"parallelizeTestCollection"')
    res.append(run_case('--other: a JSON key renamed', A, edit(Bot, jf, j_key2), 'refused', other=True))
    def j_sub(bom, body):
        return bom, body.replace('BgmPlayer', 'MusicPlayer', 1)
    res.append(run_case('--other: the _comment with a wrong name', A, edit(Bot, jf, j_sub), 'refused', other=True))
    def x_out(bom, body):
        return bom, body.replace('CasaEngine.Compiler.csproj', 'CasaEngine.Compiler2.csproj', 1)
    res.append(run_case('--other: the XML changed outside its comment', A, edit(Bot, cf, x_out), 'refused', other=True))
    def x_com(bom, body):
        return bom, body.replace('YarnBindingsTests', 'YarnBindingTests', 1)
    res.append(run_case('--other: the XML comment with a wrong name', A, edit(Bot, cf, x_com), 'refused', other=True))
    res.append(run_case('--other: departure -> departure', A, A, 'refused', other=True))
    res.append(run_case('--other: a .cs changed', A, edit(Bot, f, m_code), 'refused', other=True, why='outside the scope'))

    # the independent reader (Roslyn), when built: accepts the arrival, refuses a string literal changed
    if os.path.exists(P.XCHECK_DLL):
        ok1 = P.xcheck(A, Bcs, os.path.join(work, 'xc1')) == []
        ok2 = P.xcheck(A, edit(Bcs, f, m_lit), os.path.join(work, 'xc2')) != []
        print('%s comment-xcheck accepts the arrival; %s it refuses a literal changed' % ('ok  ' if ok1 else 'FAIL', 'ok  ' if ok2 else 'FAIL'))
        res += [ok1, ok2]
    else:
        print('comment-xcheck not built: its two cases are skipped')

    bad = res.count(False)
    print('%d cases, %d wrong' % (len(res), bad))
    return 1 if bad else 0


if __name__ == '__main__':
    sys.exit(main())
