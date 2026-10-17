"""Proof of the R3 commits (docs/plan-restructuration-dll.md, section 7, T0 (c)). Python 3, standard library only. Read-only.

  python -B tools/dll-structure/prove_r3.py --domain <D> --kind move   [--part 1|2] [--commit REV]
  python -B tools/dll-structure/prove_r3.py --domain <D> --kind rename [--commit REV]
  python -B tools/dll-structure/prove_r3.py --domain <D> --kind rename --worktree              (HEAD against the working tree)
  python -B tools/dll-structure/prove_r3.py --domain <D> --kind rename --before-dir X --after-dir Y        (two trees; proofs of the tool)
  python -B tools/dll-structure/prove_r3.py --all --base REV

REV defaults to HEAD (compared with its parent). Files are paired by path; the table (table.json) gives the files that move and the names that change.

kind move: the commit holds nothing but renames of the files of the domain, exactly those of the table (all of them, or the part named by
--part: the two commits of Events, `EventProgramRunner.cs` -> `IEventProgramRunner.cs` first, then the rest), each at 100 % (the very bytes).
kind rename: no path changes, and each file is checked OCCURRENCE BY OCCURRENCE. A C# lexer separates the code, the strings (regular, verbatim,
raw, interpolated and their holes), the characters, the preprocessor lines and the comments. Then, in a file of before, the expected after is
rebuilt and compared with the real after, to the byte:
  - code (the `cref` values and `nameof` included): an old name of a type of the domain is replaced by its r3 name, preceded by nothing or by
    one qualification that Roslyn adds (the namespace of the type, or its last segments, or `global::` + the namespace); nothing else changes;
  - strings: an occurrence of an old name changes only if the survey (r3-literals.tsv) has an entry of class 1, 1b or 3 for the domain, and then
    exactly into the name the survey gives; an occurrence the survey does not list is a refusal; all the rest of the literal stays;
  - comments, characters and every other byte: unchanged.
Every entry of the survey for the domain (class 1, 1b, 3, new name different) must be applied (an entry not applied is a refusal).
--all: every commit of BASE..HEAD by its subject (`rename the X files after their types (R3, git mv only)`, `drop the Alundra prefix in the X
domain (R3)`), a commit that touches Alundra/, Alundra.Tests/ or tools/AlundraTestSaves/ under another subject fails; then the end state: no old
name of a renamed type left in code or in a literal (but the entries of class 2 or 4), every moved file at its r3 path, the survey complete,
each domain done once (Events moves twice).
Exit 0 when everything holds, 1 otherwise (every violation is listed), 2 on a usage or reading error.
"""
import argparse
import os
import re
import subprocess
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import apply_r2 as A  # noqa: E402
import r3_literals as L  # noqa: E402

REPO = A.REPO
ROOTS = L.ROOTS
IDENT_RE = re.compile(r'[A-Za-z_]\w*')
MOVE_RE = re.compile(r'^refactor\(dll\): rename the (\w+) files after their types \(R3, git mv only\)')
RENAME_RE = re.compile(r'^refactor\(dll\): drop the Alundra prefix in the (\w+) domain \(R3\)')
EVENTS_FIRST = 'Alundra/Events/EventProgramRunner.cs'


class Report:
    def __init__(self):
        self.problems = []
        self.stats = {}

    def bad(self, msg):
        self.problems.append(msg)

    def count(self, key, n=1):
        self.stats[key] = self.stats.get(key, 0) + n


# ---------------------------------------------------------------------------------------------------- the trees
def gitb(*args, input=None):
    r = subprocess.run(['git', '-C', REPO, *args], capture_output=True, input=input)
    if r.returncode != 0:
        raise A.R2Error('git %s: %s' % (' '.join(args), r.stderr.decode('utf-8', 'replace').strip()))
    return r.stdout


def in_roots(p):
    return p.endswith('.cs') and any(p.startswith(r + '/') for r in ROOTS) and not A.EXCLUDED_DIR_RE.search(p)


def read_rev(rev):
    """{path: bytes} of the .cs files of the three roots at a revision (ls-tree, then one cat-file --batch)."""
    out = gitb('ls-tree', '-r', '-z', rev, '--', *ROOTS).split(b'\0')
    items = []
    for ent in out:
        if not ent:
            continue
        meta, path = ent.split(b'\t', 1)
        sha = meta.split()[2].decode()
        p = path.decode('utf-8')
        if in_roots(p):
            items.append((p, sha))
    data = gitb('cat-file', '--batch', input=''.join(s + '\n' for _, s in items).encode())
    res, pos = {}, 0
    for p, sha in items:
        nl = data.index(b'\n', pos)
        hdr = data[pos:nl].split()
        size = int(hdr[2])
        res[p] = data[nl + 1:nl + 1 + size]
        pos = nl + 1 + size + 1
    return res


def read_dir(root):
    res = {}
    for p in L.tree_files(root):
        with open(os.path.join(root, p), 'rb') as f:
            res[p] = f.read()
    return res


# ---------------------------------------------------------------------------------------------------- the table
class Ctx:
    def __init__(self):
        self.info = L.Info()
        self.entries = L.entries_by_key(L.load_tsv())
        self.rows_by_old = {}
        for r in self.info.tb.rows:
            self.rows_by_old.setdefault(r['old_name'], r)

    def domain_names(self, domain):
        """{old name: r3 name} of the types renamed in R3 whose domain is `domain`."""
        return {o: n for o, n in self.info.renamed.items() if self.info.domain_of_name[o] == domain}

    def expected_moves(self, domain):
        """{r2 path: r3 path} of the files renamed in R3 whose r3 folder is in `domain`."""
        return {o: n for o, n in self.info.r2_to_r3.items() if A.Table.domain(n) == domain}

    def quals(self, old):
        ns = self.rows_by_old[old]['r3']['namespace']
        segs = ns.split('.')
        q = ['global::' + ns + '.'] + ['.'.join(segs[i:]) + '.' for i in range(len(segs))]
        return q  # longest first

    def domains(self):
        return sorted({self.info.domain_of_name[o] for o in self.info.renamed})


# ---------------------------------------------------------------------------------------------------- move
def prove_move(ctx, domain, before, after, part, rep):
    exp = ctx.expected_moves(domain)
    if part == 1:
        exp = {k: v for k, v in exp.items() if k == EVENTS_FIRST}
    elif part == 2:
        exp = {k: v for k, v in exp.items() if k != EVENTS_FIRST}
    if not exp:
        rep.bad('move %s: the table has no file to move for this domain/part' % domain)
        return
    dele = sorted(set(before) - set(after))
    add = sorted(set(after) - set(before))
    mod = sorted(p for p in set(before) & set(after) if before[p] != after[p])
    if mod:
        rep.bad('move %s: files modified (only renames are allowed): %s' % (domain, mod[:5]))
    if set(dele) != set(exp):
        rep.bad('move %s: deleted paths differ from the table: missing %s, unexpected %s' % (
            domain, sorted(set(exp) - set(dele))[:5], sorted(set(dele) - set(exp))[:5]))
    if set(add) != set(exp.values()):
        rep.bad('move %s: added paths differ from the table: missing %s, unexpected %s' % (
            domain, sorted(set(exp.values()) - set(add))[:5], sorted(set(add) - set(exp.values()))[:5]))
    for o, n in exp.items():
        if o in before and n in after and before[o] != after[n]:
            rep.bad('move %s: %s -> %s is not a 100%% rename' % (domain, o, n))
    rep.count('files moved', len(exp))


# ---------------------------------------------------------------------------------------------------- rename
def line_of(text, off):
    return text.count('\n', 0, off) + 1


def first_diff(a, b):
    n = min(len(a), len(b))
    i = 0
    while i < n and a[i] == b[i]:
        i += 1
    return i


def doc_cref_values(src):
    """[(start, value)] of the cref attributes inside the DOCUMENTATION comments (`///`, `/**`) only: a plain `//` comment is not a doc comment,
    Roslyn does not bind its `cref` and the proof leaves it alone, like any other comment."""
    out = []
    for a, b, k in src.spans:
        if k == 'c' and src.text.startswith(('///', '/**'), a):
            for m in A.CREF_RE.finditer(src.text, a, b):
                out.append((m.start(1), m.group(1)))
    return out


def walk(ctx, domain, path, bbody, abody, dm, applied, rep):
    info = ctx.info
    src = A.Src(bbody)
    events = []
    for m in IDENT_RE.finditer(src.masked):
        w = m.group(0)
        if w in dm:
            events.append((m.start(), m.end(), 'code', dm[w], w))
    for start, val in doc_cref_values(src):
        for m in IDENT_RE.finditer(val):
            if m.group(0) in dm:
                events.append((start + m.start(), start + m.end(), 'cref', dm[m.group(0)], m.group(0)))
    canon = info.canon(path)
    for a, b, k in src.spans:
        if k != 's' or bbody.startswith("'", a):
            continue
        line = line_of(bbody, a)
        for m in info.name_rx.finditer(bbody, a, b):
            w = m.group(1)
            if w not in info.renamed:
                continue
            e = ctx.entries.get((canon, line, w))
            if e is None:
                rep.bad('%s:%d: a literal names %s and the survey does not list it' % (path, line, w))
                continue
            if e['class'] in L.APPLIED_CLASSES and e['domain'] == domain and e['new_name'] != w:
                events.append((m.start(1), m.end(1), 'lit', e['new_name'], w))
                applied.add((canon, line, w))
    events.sort()
    pa = pb = 0
    used_q = 0
    for s, e, kind, new, old in events:
        seg = bbody[pb:s]
        if abody[pa:pa + len(seg)] != seg:
            d = first_diff(abody[pa:], seg)
            rep.bad('%s:%d: unexpected change near %r (expected %r)' % (path, line_of(abody, pa + d), abody[pa + d:pa + d + 30], seg[d:d + 30]))
            return
        pa += len(seg)
        if kind in ('code', 'cref'):
            for q in ctx.quals(old) + ['']:
                if abody.startswith(q + new, pa):
                    pa += len(q) + len(new)
                    if q:
                        used_q += 1
                    break
            else:
                rep.bad('%s:%d: `%s` is not rewritten as `%s` (alone or qualified by its namespace): %r' % (
                    path, line_of(bbody, s), old, new, abody[pa:pa + 40]))
                return
            rep.count('code occurrences' if kind == 'code' else 'cref occurrences')
        else:
            if not abody.startswith(new, pa):
                rep.bad('%s:%d: `%s` is not rewritten as `%s` (%s): %r' % (path, line_of(bbody, s), old, new, kind, abody[pa:pa + 40]))
                return
            pa += len(new)
            rep.count('literal occurrences')
        pb = e
    if abody[pa:] != bbody[pb:]:
        d = first_diff(abody[pa:], bbody[pb:])
        rep.bad('%s:%d: unexpected change near %r (expected %r)' % (path, line_of(abody, pa + d), abody[pa + d:pa + d + 30], bbody[pb + d:pb + d + 30]))
        return
    if events or bbody != abody:
        rep.count('files changed')
    rep.count('qualifications', used_q)


def decode(b):
    t = b.decode('latin-1')
    return A.split_bom(t)


def prove_rename(ctx, domain, before, after, rep):
    dm = ctx.domain_names(domain)
    if not dm:
        rep.bad('rename %s: the table renames no type of this domain' % domain)
        return
    if set(before) != set(after):
        rep.bad('rename %s: the set of files changed: removed %s, added %s' % (
            domain, sorted(set(before) - set(after))[:5], sorted(set(after) - set(before))[:5]))
    applied = set()
    for p in sorted(set(before) & set(after)):
        bb, ab = decode(before[p]), decode(after[p])
        if bb[0] != ab[0]:
            rep.bad('%s: the BOM changed' % p)
            continue
        walk(ctx, domain, p, bb[1], ab[1], dm, applied, rep)
    # every entry of the survey for the domain is applied
    for k, e in ctx.entries.items():
        if e['class'] in L.APPLIED_CLASSES and e['domain'] == domain and e['new_name'] != e['name'] and k not in applied:
            rep.bad('%s:%d: the survey entry %s -> %s (class %s) was not found in the files, so it was not applied' % (
                e['file'], e['line'], e['name'], e['new_name'], e['class']))
    rep.count('types', len(dm))
    rep.count('survey entries applied', len(applied))


# ---------------------------------------------------------------------------------------------------- the whole range
def tranche_changed(rev):
    out = gitb('diff-tree', '-r', '--no-renames', '--name-only', '-z', rev + '^', rev).split(b'\0')
    return [p.decode('utf-8') for p in out if p and in_roots(p.decode('utf-8'))]


def end_state(ctx, rep):
    tree = read_dir(REPO)
    info = ctx.info
    allren = ctx.info.renamed
    for p, b in tree.items():
        body = decode(b)[1]
        src = A.Src(body)
        for m in IDENT_RE.finditer(src.masked):
            if m.group(0) in allren:
                rep.bad('end state: %s:%d: the old name %s remains in code' % (p, line_of(body, m.start()), m.group(0)))
        for start, val in doc_cref_values(src):
            for m in IDENT_RE.finditer(val):
                if m.group(0) in allren:
                    rep.bad('end state: %s:%d: the old name %s remains in a cref' % (p, line_of(body, start), m.group(0)))
    for pr in L.check(info, REPO, 'after'):
        rep.bad('end state: ' + pr)
    for o, n in info.r2_to_r3.items():
        if not os.path.isfile(os.path.join(REPO, n)):
            rep.bad('end state: %s is missing' % n)
        if os.path.isfile(os.path.join(REPO, o)) and o != n:
            rep.bad('end state: %s still exists' % o)


def prove_all(ctx, base, rep):
    revs = gitb('rev-list', '--reverse', '%s..HEAD' % base).decode().split()
    moved = {}   # domain -> set of moved r2 paths
    renamed_domains = []
    for rev in revs:
        subject = gitb('log', '-1', '--format=%s', rev).decode('utf-8', 'replace').strip()
        mv, rn = MOVE_RE.match(subject), RENAME_RE.match(subject)
        if mv:
            d = mv.group(1)
            before, after = read_rev(rev + '^'), read_rev(rev)
            dele = set(before) - set(after)
            part = None
            if d == 'Events':
                part = 1 if EVENTS_FIRST in dele else 2
                if part == 2 and EVENTS_FIRST not in moved.get(d, set()):
                    rep.bad('Events: the second move comes before the first')
            prove_move(ctx, d, before, after, part, rep)
            moved.setdefault(d, set()).update(dele)
        elif rn:
            d = rn.group(1)
            if d in renamed_domains:
                rep.bad('domain %s renamed twice' % d)
            renamed_domains.append(d)
            exp = set(ctx.expected_moves(d))
            if exp and moved.get(d, set()) != exp:
                rep.bad('domain %s: the file moves before the rename do not cover the table (missing %s)' % (d, sorted(exp - moved.get(d, set()))[:3]))
            prove_rename(ctx, d, read_rev(rev + '^'), read_rev(rev), rep)
        else:
            touched = tranche_changed(rev)
            if touched:
                rep.bad('commit %s (%s) touches %s under another subject' % (rev[:8], subject[:60], touched[:3]))
    missing = [d for d in ctx.domains() if d not in renamed_domains]
    if missing:
        rep.bad('domains not renamed: %s' % missing)
    end_state(ctx, rep)
    return len(revs)


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument('--domain')
    ap.add_argument('--kind', choices=('move', 'rename'))
    ap.add_argument('--part', type=int, choices=(1, 2))
    ap.add_argument('--commit', default='HEAD')
    ap.add_argument('--worktree', action='store_true')
    ap.add_argument('--before-dir')
    ap.add_argument('--after-dir')
    ap.add_argument('--all', action='store_true')
    ap.add_argument('--base')
    a = ap.parse_args()
    sys.stdout.reconfigure(encoding='utf-8')
    rep = Report()
    try:
        ctx = Ctx()
        if a.all:
            if not a.base:
                ap.error('--all needs --base')
            n = prove_all(ctx, a.base, rep)
            what = '%d commit(s) and the end state' % n
        else:
            if not a.domain or not a.kind:
                ap.error('--domain and --kind')
            if a.before_dir and a.after_dir:
                before, after = read_dir(a.before_dir), read_dir(a.after_dir)
            elif a.worktree:
                before, after = read_rev('HEAD'), read_dir(REPO)
                # git stores LF (core.autocrlf=true) while the working tree may hold CRLF: compare what git would commit
                after = {k: v.replace(b'\r\n', b'\n') for k, v in after.items()}
            else:
                before, after = read_rev(a.commit + '^'), read_rev(a.commit)
            if a.kind == 'move':
                prove_move(ctx, a.domain, before, after, a.part, rep)
            else:
                prove_rename(ctx, a.domain, before, after, rep)
            what = '%s %s' % (a.kind, a.domain)
    except A.R2Error as e:
        print('ERROR: %s' % e)
        return 2
    for p in rep.problems:
        print(p)
    st = ', '.join('%d %s' % (v, k) for k, v in sorted(rep.stats.items()))
    print('prove_r3: %s (%s) -> %s' % (what, st, 'OK' if not rep.problems else '%d violation(s)' % len(rep.problems)))
    return 0 if not rep.problems else 1


if __name__ == '__main__':
    sys.exit(main())
