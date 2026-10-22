"""R5 (D-R-21 (3)): proof of the commit that renames the nested test classes of r5-nested-types.tsv.

  python -B tools/dll-structure/prove_r5_nested.py [--commit REV] [--repo <folder>]

The commit (default HEAD, against its parent) must change exactly the files of the survey, and each of them only by the simple name of its nested class:
read the new file, put the old name back wherever the new name stands as a whole identifier, and the result must equal the old file byte for byte.
Every changed line must hold the old name (declaration or reference), the old name must stand nowhere in the tracked .cs files of the repository
afterwards, and the new name must stand at least once in the file of the survey. Read-only. Exit 0 = proven, 1 = refused (every finding listed), 2 = usage error.
"""
import argparse
import csv
import re
import subprocess
import sys
from pathlib import Path

HERE = Path(__file__).resolve().parent


def git(repo, *args):
    r = subprocess.run(['git', '-C', str(repo)] + list(args), capture_output=True)
    if r.returncode != 0:
        raise SystemExit('git %s failed: %s' % (' '.join(args), r.stderr.decode('utf-8', 'replace')))
    return r.stdout


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument('--commit', default='HEAD')
    ap.add_argument('--repo', default=str(HERE.parent.parent))
    ap.add_argument('--survey', default=str(HERE / 'r5-nested-types.tsv'))
    a = ap.parse_args()
    repo = Path(a.repo)
    rows = list(csv.DictReader(open(a.survey, encoding='utf-8', newline=''), delimiter='\t'))
    if not rows:
        print('ERROR: empty survey')
        return 2
    findings = []
    changed = git(repo, 'diff', '--name-status', '--no-renames', '-z', a.commit + '^', a.commit).decode('utf-8').split('\0')
    changed = [(changed[i], changed[i + 1]) for i in range(0, len(changed) - 1, 2)]
    want = sorted({r['new_file'] for r in rows})
    got = sorted(p for _, p in changed)
    if got != want:
        findings.append('changed files %s, the survey says %s' % (got, want))
    if any(s != 'M' for s, _ in changed):
        findings.append('a change that is not a modification: %s' % [c for c in changed if c[0] != 'M'])
    for r in rows:
        path, old, new = r['new_file'], r['old_type'], r['new_type']
        if path not in got:
            continue
        before = git(repo, 'show', '%s^:%s' % (a.commit, path))
        after = git(repo, 'show', '%s:%s' % (a.commit, path))
        pat = re.compile(rb'(?<![A-Za-z0-9_])' + re.escape(new.encode()) + rb'(?![A-Za-z0-9_])')
        if pat.sub(old.encode(), after) != before:
            findings.append('%s: the file is not the old one with %s put back for %s' % (path, old, new))
        if len(pat.findall(after)) < 1:
            findings.append('%s: the new name %s does not stand in the file' % (path, new))
        bl, al = before.split(b'\n'), after.split(b'\n')
        if len(bl) != len(al):
            findings.append('%s: the line count changed (%d -> %d)' % (path, len(bl), len(al)))
        else:
            oldpat = re.compile(rb'(?<![A-Za-z0-9_])' + re.escape(old.encode()) + rb'(?![A-Za-z0-9_])')
            for i, (x, y) in enumerate(zip(bl, al), 1):
                if x != y and not oldpat.search(x):
                    findings.append('%s:%d: a changed line without the old name' % (path, i))
        # the old name stands nowhere in the tracked sources afterwards
        g = subprocess.run(['git', '-C', str(repo), 'grep', '-l', '-w', '-e', old, a.commit, '--', '*.cs'], capture_output=True)
        if g.returncode not in (0, 1):   # 1 = no match
            raise SystemExit('git grep failed: ' + g.stderr.decode('utf-8', 'replace'))
        hits = g.stdout.decode('utf-8', 'replace').split()
        if hits:
            findings.append('the old name %s still stands in %s' % (old, hits))
    if findings:
        print('REFUSED (%d finding(s)):' % len(findings))
        for f in findings:
            print('  ' + f)
        return 1
    print('PROVEN: %s changes only the nested class name(s) %s at the declaration and the references (%d file(s))' % (
        a.commit, ', '.join('%s -> %s' % (r['old_type'], r['new_type']) for r in rows), len(got)))
    return 0


if __name__ == '__main__':
    sys.exit(main())
