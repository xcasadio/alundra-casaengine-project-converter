"""R8 (T1, T2): applies the survey `r8-comments.tsv` to the comments of the DLL and of the tests.

    python -B tools/dll-structure/apply_r8.py [--scope dll|tests|all] [--repo DIR] [--dry-run]    the `.cs` files (default scope: all)
    python -B tools/dll-structure/apply_r8.py --other [--repo DIR] [--dry-run]                    xunit.runner.json (the value of `_comment`)
                                                                                                    and Alundra.Tests.csproj (its XML comment)

Edits only the comment regions (the lexer of R2, kind `c`; for the two other files, the `_comment` value and the XML comments), one event per
candidate of the survey; reads and writes bytes (BOM, line endings and every other byte kept).  Before writing a file it checks that the
candidates found in it and the rows of the survey are the same (same line, same rank, same old text); on the least difference it writes
NOTHING and exits 2.  Rows of class `kept` are not touched.  Writes the working tree; commits nothing.
"""
import argparse
import sys

import r8_lib as L


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('--scope', default='all', choices=['dll', 'tests', 'all'])
    ap.add_argument('--other', action='store_true')
    ap.add_argument('--repo', default=L.REPO)
    ap.add_argument('--dry-run', action='store_true')
    a = ap.parse_args()
    survey = L.load_survey()
    if a.other:
        paths = list(L.OTHER_FILES)
    else:
        paths = [p for p in (L.list_cs_git() if a.repo == L.REPO else L.list_cs_dir(a.repo)) if L.scope_ok(p, a.scope)]
    plan, errs, events = {}, [], 0
    for path in paths:
        bom, body = L.decode(L.read_bytes(a.repo, path))
        cands = L.other_candidates(path, body) if a.other else L.cs_candidates(path, body)
        rows = survey.get(path, {})
        errs += L.match_rows(path, cands, rows)
        if errs:
            continue
        for c in cands:
            if c.cls == 'stop':
                errs.append('%s:%d: undecided candidate %r' % (path, c.line, c.old))
        new_body, n = L.rewrite(body, cands, rows)
        if n:
            if new_body.count('\n') != body.count('\n') or new_body.count('\r') != body.count('\r'):
                errs.append('%s: the line structure changed' % path)
            plan[path] = L.encode(bom, new_body)
            events += n
    if not a.other:
        # a survey row of a file of the scope that is not in the tree is an error too
        for f in survey:
            if f.endswith('.cs') and L.scope_ok(f, a.scope) and f not in paths:
                errs.append('survey file %s is not in the tree' % f)
    if errs:
        print('REFUSED, nothing written (%d):' % len(errs))
        for e in errs[:40]:
            print('  ' + e)
        return 2
    if not a.dry_run:
        for path, data in plan.items():
            L.write_bytes(a.repo, path, data)
    print('%s: %d files, %d events%s' % ('other' if a.other else a.scope, len(plan), events, ' (dry run)' if a.dry_run else ''))
    return 0


if __name__ == '__main__':
    sys.exit(main())
