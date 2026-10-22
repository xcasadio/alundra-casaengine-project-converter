"""R5 of the DLL restructuring (docs/plan-restructuration-dll.md, section 9, T0c): the tests in the mirrored tree, domain by domain.

  python -B tools/dll-structure/apply_r5.py <Domain> --move    step 1: `git mv` of the files of the domain to their path of r5-files.tsv (the file name
                                                               included; not one byte changes)
  python -B tools/dll-structure/apply_r5.py <Domain> --code    step 2, in this order: (a) the survey entries of the domain (the string literals L1, the
                                                               qualified references Q1, the captures) and the `namespace` line of the files of the domain;
                                                               (b) the cut of the secondary types (P-R5-1): the declaration, with its documentation
                                                               comments, attributes and ONE leading blank line, copied byte for byte into a new file at
                                                               its path of r5-files.tsv, with the header of the file where it is at that moment (its
                                                               `using` lines, its `#nullable`) and the `namespace` of its arrival, and removed from its
                                                               source; (c) the `using` lines recomputed from the CURRENT namespace of each test type
                                                               (every file of Alundra.Tests); (d) the rename tool (rename-tool/, --r5 <Domain>) renames
                                                               the classes of the domain (Alundra prefix dropped) with Roslyn
  python -B tools/dll-structure/apply_r5.py --domains          the domains, in order

Options: --dry-run (writes nothing, no rename), --no-rename (stops before (d), for the proofs of the tool), --allow-dirty, --repo <dir> (a
throw-away copy; its engine references are absolute), --only-domain (the proofs on a copy: only this domain counts as done), --tables <dir>.
Nothing is committed (the executor commits by path); new files are `git add`ed. The files keep their line endings and their encoding byte for
byte (read and written as latin-1), but for the lines changed. The data file `Alundra.Tests/Data/story-chain-skipped-opcodes.tsv` and the literal
that names it never change (D-R-13 point 6).
"""
import argparse
import os
import re
import subprocess
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import r5_lib as L  # noqa: E402

A = L.A


class Err(L.R5Error):
    pass


def set_repo(path):
    A.REPO = path
    L.REPO = path


def check_tree_clean(allow_dirty):
    out = L.git('status', '--porcelain=v1', '--untracked-files=all').decode('utf-8', 'replace')
    bad_outside, dirty_inside = [], []
    for line in out.splitlines():
        p = line[3:]
        if ' -> ' in p:
            p = p.split(' -> ')[1]
        p = p.strip('"')
        if p in A.SUBMODULES or any(p.startswith(s + '/') for s in A.SUBMODULES):
            continue
        if p.startswith('Alundra.Tests/'):
            dirty_inside.append(line)
        elif p.startswith('tools/dll-structure/') or p == 'docs/plan-restructuration-dll.md':
            continue
        elif p.startswith('tools/Convert-') or p.startswith('tools/Saves/'):
            continue   # the author's own untracked files, never touched
        else:
            bad_outside.append(line)
    if bad_outside:
        raise Err('the tree is not clean outside the paths of the slice:\n  ' + '\n  '.join(bad_outside))
    if dirty_inside and not allow_dirty:
        raise Err('Alundra.Tests is not clean (use --allow-dirty to go on):\n  ' + '\n  '.join(dirty_inside[:20]))


def do_move(tbl, domain, dry):
    rows = [r for r in tbl.files if r['kind'] == 'move' and r['domain'] == domain and r['old_path'] != r['new_path']]
    if not rows:
        print('nothing to move for %s' % domain)
        return 0
    for r in rows:
        if not os.path.isfile(os.path.join(A.REPO, r['old_path'])):
            raise Err('source file missing: %s' % r['old_path'])
        if os.path.exists(os.path.join(A.REPO, r['new_path'])):
            raise Err('target already exists: %s' % r['new_path'])
    for r in sorted(rows, key=lambda r: r['old_path']):
        print('git mv %s -> %s' % (r['old_path'], r['new_path']))
        if not dry:
            os.makedirs(os.path.dirname(os.path.join(A.REPO, r['new_path'])), exist_ok=True)
            L.git('mv', r['old_path'], r['new_path'])
    if not dry:   # git leaves the folders it emptied (Scripts/, UI/ at the end): remove those that are empty, never one that holds anything
        for d in sorted({os.path.dirname(r['old_path']) for r in rows}, key=len, reverse=True):
            if d != 'Alundra.Tests':
                try:
                    os.rmdir(os.path.join(A.REPO, d))
                except OSError:
                    pass
    print('%d file(s) moved' % len(rows))
    return 0


def test_files_now():
    return sorted(p for p in L.git('ls-files', '--cached', '--others', '--exclude-standard', '--', 'Alundra.Tests').decode('utf-8').splitlines()
                  if p.endswith('.cs') and not A.EXCLUDED_DIR_RE.search(p) and os.path.isfile(os.path.join(A.REPO, p)))


def own_namespaces(ns):
    parts = (ns or '').split('.')
    return {'.'.join(parts[:i]) for i in range(1, len(parts) + 1)}


def insert_usings(bom, body, src, add):
    """The body with `using <ns>;` lines added after the last using of the head (None when nothing to add)."""
    nm = A.NS_LINE_RE.search(src.masked)
    head, rest = body[:nm.start()], body[nm.start():]
    lines = head.split('\n')
    existing = {m.group(1) for m in (A.USING_RE.match(l.rstrip('\r')) for l in lines) if m}
    add = sorted(ns for ns in add if ns not in existing)
    if not add:
        return None
    cr = '\r' if '\r\n' in body else ''
    new = [('using %s;' % ns) + cr for ns in add]
    last = max((i for i, l in enumerate(lines) if l.lstrip().startswith('using ')), default=-1)
    if last >= 0:
        lines[last + 1:last + 1] = new
    else:
        idx = len(lines) - 1
        lines[idx:idx] = new + [cr]
    return bom + '\n'.join(lines) + rest


def do_code(tbl, domain, dry, rename, only):
    done = L.R5.done(domain, only)
    files = test_files_now()
    fileset = set(files)
    cur = {}
    # the layout precondition: step 1 is done for the domain and the earlier ones
    for r in tbl.files:
        if r['kind'] == 'keep':
            continue
        if r['kind'] == 'move':
            want = tbl.path_now(r, done)
            other = r['old_path'] if want == r['new_path'] else r['new_path']
            if want not in fileset or (other != want and other in fileset):
                raise Err('step 1 is not what domain %s expects: %s should be at %s' % (domain, r['old_path'], want))
        elif r['kind'] == 'cut':
            exists = r['new_path'] in fileset
            if r['domain'] == domain and exists:
                raise Err('the cut file %s already exists' % r['new_path'])
            if r['domain'] in done and r['domain'] != domain and not exists:
                raise Err('the cut file %s of an earlier domain is missing' % r['new_path'])

    def text_of(p):
        return cur[p] if p in cur else A.read(p)

    written = {}   # path -> text
    # (a) the survey entries and the namespace lines
    ns_done = 0
    for r in tbl.files:
        if r['kind'] != 'move':
            continue
        p = tbl.path_now(r, done)
        text = text_of(p)
        bom, body = A.split_bom(text)
        src = A.Src(body)
        evs = L.entry_events(tbl, r['old_path'], src, domain)
        if r['domain'] == domain:
            evs.append(L.namespace_event(src, r['namespace']))
            ns_done += 1
        if evs:
            new_body = L.apply_events(body, evs)
            if new_body != body:
                cur[p] = bom + new_body
                written[p] = cur[p]
    n_entries = sum(1 for e in tbl.literals + tbl.qualified if e['domain'] == domain)
    print('%s: %d survey entr%s, %d namespace line(s)' % (domain, n_entries, 'y' if n_entries == 1 else 'ies', ns_done))

    # (b) the cuts
    new_files = {}
    by_src = {}
    for c in tbl.cuts:
        if c['domain'] == domain:
            by_src.setdefault(c['source_file'], []).append(c)
    for src_old, cuts in sorted(by_src.items()):
        sp = tbl.path_now(tbl.by_old[src_old], done)
        text = text_of(sp)
        for c in sorted(cuts, key=lambda c: c['new_path']):
            newtext, rest = A.cut_type(text, c['main_type'], c['namespace'])
            new_files[c['new_path']] = newtext
            text = rest
            print('cut %s from %s -> %s' % (c['main_type'], sp, c['new_path']))
        cur[sp] = text
        written[sp] = text

    # (c) the using lines of every file of Alundra.Tests
    name_ns = tbl.name_ns(done, unrenamed=domain)
    all_paths = sorted(set(files) | set(new_files))
    n_using = 0
    for p in all_paths:
        text = new_files[p] if p in new_files else text_of(p)
        bom, body = A.split_bom(text)
        src = A.Src(body)
        own = A.file_namespace(text)
        enclosing = own_namespaces(own)
        needed = {name_ns[n] for n in src.names() if n in name_ns and name_ns[n] not in enclosing}
        out = insert_usings(bom, body, src, needed)
        if out is not None:
            n_using += 1
            if p in new_files:
                new_files[p] = out
            else:
                cur[p] = out
                written[p] = out
    print('%s: using lines added to %d file(s)' % (domain, n_using))

    if dry:
        print('dry run: nothing written (%d file(s) changed, %d new)' % (len(written), len(new_files)))
        return 0
    for p, t in written.items():
        if p in new_files:
            continue
        A.write(p, t)
    for p, t in new_files.items():
        A.write(p, t)
        L.git('add', '--', p)
    print('%s --code: %d file(s) written, %d new' % (domain, len(written), len(new_files)))
    if not rename:
        return 0
    return run_rename(domain)


def run_rename(domain):
    exe = os.path.join(HERE, 'rename-tool', 'bin', 'Debug', 'net9.0', 'RenameTool.exe')
    if not os.path.isfile(exe):
        raise Err('the rename tool is not built (dotnet build -c Debug in rename-tool/)')
    r = subprocess.run([exe, A.REPO, '--r5', domain, '--types', os.path.join(HERE, 'r5-types.tsv')])
    if r.returncode != 0:
        raise Err('the rename tool refused domain %s (exit %d): nothing was renamed; the tree holds steps (a) to (c)' % (domain, r.returncode))
    return 0


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument('domain', nargs='?')
    g = ap.add_mutually_exclusive_group()
    g.add_argument('--move', action='store_true')
    g.add_argument('--code', action='store_true')
    g.add_argument('--domains', action='store_true')
    ap.add_argument('--dry-run', action='store_true')
    ap.add_argument('--no-rename', action='store_true')
    ap.add_argument('--allow-dirty', action='store_true')
    ap.add_argument('--only-domain', action='store_true')
    ap.add_argument('--repo')
    ap.add_argument('--tables', default=HERE)
    a = ap.parse_args()
    sys.stdout.reconfigure(encoding='utf-8')
    if a.domains:
        print(' '.join(L.DOMAINS))
        return 0
    if not a.domain or a.domain not in L.DOMAINS or not (a.move or a.code):
        ap.error('a domain of %s and --move or --code' % L.DOMAINS)
    if a.repo:
        set_repo(os.path.abspath(a.repo))
    try:
        tbl = L.R5(a.tables)
        check_tree_clean(a.allow_dirty)
        if a.move:
            return do_move(tbl, a.domain, a.dry_run)
        return do_code(tbl, a.domain, a.dry_run, not a.no_rename, a.only_domain)
    except L.R5Error as e:
        print('ERROR: %s' % e)
        return 2
    except A.R2Error as e:
        print('ERROR: %s' % e)
        return 2


if __name__ == '__main__':
    sys.exit(main())
