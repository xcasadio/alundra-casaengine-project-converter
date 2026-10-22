"""R8 proof: the comments of the DLL and of the tests carry the names of today, and NOTHING else changed.

    python -B tools/dll-structure/prove_r8.py [--scope dll|tests|all] [--other] [--xcheck] (--commit REV | --worktree | --before-dir X --after-dir Y)

  --commit REV      before = REV^ (or --from FROM: a whole range), after = REV (blobs)
  --worktree        before = HEAD (blobs), after = the working tree (CRLF folded to LF, as the index stores it)
  --before-dir/--after-dir   two folders that hold `Alundra/` and `Alundra.Tests/`
  --scope           the files that must change: the DLL, the tests, or both (default: all); every other `.cs` must be byte for byte identical
  --other           the two files that are not C# INSTEAD of the `.cs` (a commit that holds both is proved by the two runs): `Alundra.Tests/xunit.runner.json` (only the value of `_comment`) and
                    `Alundra.Tests/Alundra.Tests.csproj` (only the content of its XML comment); the JSON stays valid with the same keys, the XML
                    well-formed with the same tree
  --xcheck          also the independent reader: Roslyn (comment-xcheck/, built with `dotnet build -c Debug`) compares the stream of tokens and
                    of trivia OUTSIDE the comments of every `.cs`, before and after
  --work DIR        scratch folder of --xcheck (default: a temporary folder)

For every file of the scope: the candidates found in the file BEFORE are the rows of the survey `r8-comments.tsv` (same line, same rank, same old
text); the expected file is the file before plus the events of the rows of class auto and hand, every other byte unchanged (BOM, line endings,
number of `\\n` and of `\\r`, number of lines: additions = deletions); the file after is that expected file to the byte; and the candidates still
standing AFTER are exactly the rows of class `kept`.  Exit 0 = proved, 1 = refused (every finding listed), 2 = the tool could not read.
"""
import argparse
import json
import os
import re
import subprocess
import sys
import tempfile
import xml.etree.ElementTree as ET

import r8_lib as L

XCHECK_DLL = L.HERE + '/comment-xcheck/bin/Debug/net9.0/CommentXcheck.dll'


# ------------------------------------------------------------------------------------------------------------------ the two sides
def read_commit(rev, frm=None):
    frm = frm or rev + '^'
    paths_b = set(L.list_cs_git(frm)) | set(L.OTHER_FILES)
    paths_a = set(L.list_cs_git(rev)) | set(L.OTHER_FILES)
    return L.blobs_at(frm, sorted(paths_b)), L.blobs_at(rev, sorted(paths_a))


def read_worktree():
    paths = set(L.list_cs_git()) | set(L.OTHER_FILES)
    before = L.blobs_at('HEAD', sorted(set(L.list_cs_git('HEAD')) | set(L.OTHER_FILES)))
    after = {}
    for p in paths:
        after[p] = L.read_bytes(L.REPO, p).replace(b'\r\n', b'\n')
    return before, after


def read_dir(root):
    out = {}
    for p in set(L.list_cs_dir(root)) | set(L.OTHER_FILES):
        if os.path.exists(os.path.join(root, p)):
            out[p] = L.read_bytes(root, p)
    return out


# ------------------------------------------------------------------------------------------------------------------ the proof
def first_diff(a, b):
    la, lb = a.split('\n'), b.split('\n')
    for i in range(min(len(la), len(lb))):
        if la[i] != lb[i]:
            return i + 1, la[i][:160], lb[i][:160]
    return min(len(la), len(lb)) + 1, '', ''


def xml_shape(data):
    """The tree of a project file with its comments as nodes (their text not compared)."""
    parser = ET.XMLParser(target=ET.TreeBuilder(insert_comments=True))
    root = ET.fromstring(data, parser=parser)

    def walk(n):
        if n.tag is ET.Comment:
            return ('#comment',)
        return (n.tag, tuple(sorted(n.attrib.items())), (n.text or '').strip(), tuple(walk(c) for c in n), (n.tail or '').strip())
    return walk(root)


def prove(before, after, scope, other, stats):
    errs = []
    survey = L.load_survey()
    if set(before) != set(after):
        errs.append('the two sides do not hold the same files: only before %r, only after %r' %
                    (sorted(set(before) - set(after))[:5], sorted(set(after) - set(before))[:5]))
    in_scope_files = set()
    for path in sorted(set(before) & set(after)):
        if (path in L.OTHER_FILES) != bool(other):
            continue  # the other mode proves these files (a commit that holds both is proved by the two runs)
        in_scope = True if other else L.scope_ok(path, scope)
        if not in_scope:
            if before[path] != after[path]:
                errs.append('%s: changed but outside the scope' % path)
            continue
        in_scope_files.add(path)
        b, a = before[path], after[path]
        bom_b, body_b = L.decode(b)
        bom_a, body_a = L.decode(a)
        if bom_b != bom_a:
            errs.append('%s: the BOM changed' % path)
        if body_b.count('\n') != body_a.count('\n') or body_b.count('\r') != body_a.count('\r'):
            errs.append('%s: the number of \\n or of \\r changed' % path)
        rows = survey.get(path, {})
        cands = L.other_candidates(path, body_b) if other else L.cs_candidates(path, body_b)
        e = L.match_rows(path, cands, rows)
        if e:
            errs += e
            continue
        for c in cands:
            if c.cls == 'stop':
                errs.append('%s:%d: undecided candidate %r' % (path, c.line, c.old))
        expected, n = L.rewrite(body_b, cands, rows)
        if expected != body_a:
            ln, x, y = first_diff(expected, body_a)
            errs.append('%s:%d: the file is not the file before plus the survey events\n      expected: %s\n      found:    %s' % (path, ln, x, y))
            continue
        lb, la = body_b.split('\n'), body_a.split('\n')
        if len(lb) != len(la):
            errs.append('%s: the number of lines changed' % path)
        changed = sum(1 for x, y in zip(lb, la) if x != y)
        if n:
            stats['files'] += 1
            stats['events'] += n
            stats['lines'] += changed
        # what stands after: exactly the kept rows
        left = sorted((c.line, c.old) for c in (L.other_candidates(path, body_a) if other else L.cs_candidates(path, body_a)))
        kept = sorted((r['line'], r['old']) for r in rows.values() if r['class'] == 'kept')
        if left != kept:
            errs.append('%s: after the rewrite the candidates standing are %r, the kept rows are %r' % (path, left, kept))
        if other:
            try:
                if path.endswith('.json'):
                    jb, ja = json.loads(body_b), json.loads(body_a)
                    if list(jb) != list(ja) or any(jb[k] != ja[k] for k in jb if k != '_comment'):
                        errs.append('%s: a key or a value other than _comment changed' % path)
                else:
                    if xml_shape(b) != xml_shape(a):
                        errs.append('%s: the XML tree changed outside its comments' % path)
            except Exception as ex:  # noqa: BLE001
                errs.append('%s: not valid after the rewrite: %s' % (path, ex))
    # every row of the survey of the scope must belong to a file of the scope
    for f in survey:
        if (f in L.OTHER_FILES) == bool(other) and (other or L.scope_ok(f, scope)) and f not in in_scope_files:
            errs.append('survey file %s is not on both sides' % f)
    return errs


def xcheck(before, after, work):
    if not os.path.exists(XCHECK_DLL):
        return ['comment-xcheck is not built (dotnet build -c Debug in tools/dll-structure/comment-xcheck): %s' % XCHECK_DLL]
    work = work or tempfile.mkdtemp(prefix='r8-xcheck-')
    lst = []
    for side, d in (('a', before), ('b', after)):
        for p, data in d.items():
            if not p.endswith('.cs'):
                continue
            dst = os.path.join(work, side, p)
            os.makedirs(os.path.dirname(dst), exist_ok=True)
            with open(dst, 'wb') as f:
                f.write(data)
            if side == 'a':
                lst.append(p)
    lp = os.path.join(work, 'list.txt')
    with open(lp, 'w', encoding='utf-8') as f:
        f.write('\n'.join(sorted(lst)) + '\n')
    r = subprocess.run(['dotnet', XCHECK_DLL, 'equiv', os.path.join(work, 'a'), os.path.join(work, 'b'), lp], capture_output=True, text=True)
    out = (r.stdout + r.stderr).strip()
    m = re.search(r'equiv: files (\d+), textually different (\d+), not equivalent (\d+)', out)
    if r.returncode != 0 or not m:
        return ['comment-xcheck failed: ' + out[-400:]]
    print('xcheck: files %s, textually different %s, not equivalent %s' % m.groups())
    if int(m.group(3)) != 0 or int(m.group(1)) != len(lst):
        return ['comment-xcheck: ' + out[-600:]]
    return []


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('--scope', default='all', choices=['dll', 'tests', 'all'])
    ap.add_argument('--other', action='store_true')
    ap.add_argument('--xcheck', action='store_true')
    ap.add_argument('--work')
    g = ap.add_mutually_exclusive_group(required=True)
    g.add_argument('--commit')
    ap.add_argument('--from', dest='frm', help='with --commit: the state before is REV instead of the parent (a whole range)')
    g.add_argument('--worktree', action='store_true')
    g.add_argument('--before-dir')
    ap.add_argument('--after-dir')
    a = ap.parse_args()
    try:
        if a.commit:
            before, after = read_commit(a.commit, a.frm)
        elif a.worktree:
            before, after = read_worktree()
        else:
            if not a.after_dir:
                ap.error('--before-dir needs --after-dir')
            before, after = read_dir(a.before_dir), read_dir(a.after_dir)
    except (RuntimeError, OSError) as ex:
        print('cannot read: %s' % ex)
        return 2
    stats = {'files': 0, 'events': 0, 'lines': 0}
    errs = prove(before, after, a.scope, a.other, stats)
    if a.xcheck and not errs:
        errs += xcheck(before, after, a.work)
    if errs:
        print('REFUSED (%d):' % len(errs))
        for e in errs[:60]:
            print('  ' + e)
        return 1
    print('PROVED %s: %d files changed, %d events, %d lines (additions = deletions); nothing else changed' %
          ('other' if a.other else a.scope, stats['files'], stats['events'], stats['lines']))
    return 0


if __name__ == '__main__':
    sys.exit(main())
