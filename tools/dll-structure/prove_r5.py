"""Proof of the R5 commits (docs/plan-restructuration-dll.md, section 9, T0c). Python 3, standard library only. Read-only.

  python -B tools/dll-structure/prove_r5.py --domain <D> --kind move [--commit REV | --index]
  python -B tools/dll-structure/prove_r5.py --domain <D> --kind code [--commit REV | --worktree | --before-dir X --after-dir Y]
  options: --only-domain (only <D> counts as done: the proofs on a throw-away copy), --tables <dir>, --repo <dir>

REV defaults to HEAD (compared with its parent). Files are paired by the tables (r5-files.tsv), never by the rename detection of git.

kind move (step 1): the commit holds nothing but renames: the deleted paths are the old paths of the files of the domain, the added paths their
new paths, and each added blob is the very blob of the deleted file (100 %). Nothing else changes (the data file included).
kind code (step 2): the added files are exactly the new files of the domain (the secondary types cut by P-R5-1) and no file is deleted; every
other changed path is a .cs file of Alundra.Tests. For each file the expected text is rebuilt from the text of before and compared with the real
text of after, OCCURRENCE BY OCCURRENCE, with the lexer of R2 (code, strings, characters, comments, preprocessor lines):
  - a file that loses a type: it loses exactly the block (the declaration with its documentation, attributes and one leading blank line) of the
    types the table cuts from it in this domain; each new file is the header of the file it comes from (everything up to its `namespace` line)
    plus that block, byte for byte;
  - the `namespace` line of a file of the domain becomes the namespace of the table; no other namespace line changes;
  - the survey entries of the domain (literals L1, qualified references Q1, captures), located by their OCCURRENCE in the text of before, change
    exactly as the survey says, and each of them MUST change; an entry in a cut block follows the block into its new file, an entry in the
    header is in the header of every file made from it;
  - code (the `cref` values and `nameof` included): an old name of a class of the domain becomes its new name, alone or preceded by one
    qualification that Roslyn adds (the namespace of the class, or its last segments, or `global::` + the namespace); nothing else changes;
  - the head (everything up to and including the `namespace` line) differs from the expected one only by `using Alundra.Tests...;` lines that
    were added and that the file justifies (it names a test type of that namespace); no using line is removed but the dropped line of a survey
    entry; comments, other strings and every other byte are unchanged.
Exit 0 when everything holds, 1 otherwise (every violation is listed), 2 on a usage or reading error.
"""
import argparse
import os
import re
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import r5_lib as L  # noqa: E402

A = L.A
IDENT_RE = re.compile(r'[A-Za-z_]\w*')
USING_ADD_RE = re.compile(r'^using (Alundra\.Tests(?:\.\w+)*);\s*$')


class Report:
    def __init__(self):
        self.problems = []
        self.stats = {}

    def bad(self, msg):
        self.problems.append(msg)

    def count(self, key, n=1):
        self.stats[key] = self.stats.get(key, 0) + n


def line_of(text, off):
    return text.count('\n', 0, off) + 1


def first_diff(a, b):
    n = min(len(a), len(b))
    i = 0
    while i < n and a[i] == b[i]:
        i += 1
    return i


def doc_cref_values(src):
    """[(start, value)] of the cref attributes inside the DOCUMENTATION comments (`///`, `/**`) only (a plain comment is not bound by Roslyn)."""
    out = []
    for a, b, k in src.spans:
        if k == 'c' and src.text.startswith(('///', '/**'), a):
            for m in A.CREF_RE.finditer(src.text, a, b):
                out.append((m.start(1), m.group(1)))
    return out


# ---------------------------------------------------------------------------------------------------- git
def diff_raw(parent, rev):
    """[(status, path, old_sha, new_sha)] without rename detection; rev None = the index against HEAD."""
    if rev is None:
        raw = L.git('diff', '--cached', '--no-renames', '--raw', '-z', 'HEAD').split(b'\0')
    else:
        raw = L.git('diff-tree', '-r', '--no-renames', '-z', parent, rev).split(b'\0')
    out, i = [], 0
    if raw and raw[0] and not raw[0].startswith(b':'):
        i = 1
    while i < len(raw) and raw[i]:
        meta = raw[i].decode()
        f = meta[1:].split()
        out.append((f[4][0], raw[i + 1].decode('utf-8'), f[2], f[3]))
        i += 2
    return out


def worktree_changed():
    out = L.git('status', '--porcelain=v1', '-z', '--untracked-files=all').decode('utf-8', 'replace').split('\0')
    res, i = [], 0
    while i < len(out):
        e = out[i]
        i += 1
        if not e:
            continue
        st, p = e[:2], e[3:]
        if st[0] in 'RC':
            i += 1
        res.append((st, p))
    return res


# ---------------------------------------------------------------------------------------------------- move
def prove_move(tbl, domain, ch, rep):
    exp = {r['old_path']: r['new_path'] for r in tbl.files if r['kind'] == 'move' and r['domain'] == domain and r['old_path'] != r['new_path']}
    dele = {p: s for st, p, s, _ in ch if st == 'D'}
    add = {p: s for st, p, _, s in ch if st == 'A'}
    other = [(st, p) for st, p, _, _ in ch if st not in 'DA']
    if other:
        rep.bad('move %s: files modified (only renames are allowed): %s' % (domain, other[:5]))
    if set(dele) != set(exp):
        rep.bad('move %s: deleted paths differ from the table: missing %s, unexpected %s' % (domain, sorted(set(exp) - set(dele))[:5], sorted(set(dele) - set(exp))[:5]))
    if set(add) != set(exp.values()):
        rep.bad('move %s: added paths differ from the table: missing %s, unexpected %s' % (
            domain, sorted(set(exp.values()) - set(add))[:5], sorted(set(add) - set(exp.values()))[:5]))
    for o, n in exp.items():
        if o in dele and n in add and dele[o] != add[n]:
            rep.bad('move %s: %s -> %s is not a 100%% rename' % (domain, o, n))
    rep.count('files moved', len(exp))


# ---------------------------------------------------------------------------------------------------- code
class Ctx:
    def __init__(self, tbl, domain, only):
        self.tbl = tbl
        self.domain = domain
        self.done = L.R5.done(domain, only)
        self.dm = tbl.dm(domain)
        for o, n in list(self.dm.items()):   # the short form of an attribute class
            if o.endswith('Attribute') and n.endswith('Attribute') and len(o) > 9 and len(n) > 9:
                self.dm.setdefault(o[:-9], n[:-9])
        self.new_ns = {}
        for t in tbl.types:
            if t['domain'] == domain:
                self.new_ns.setdefault(t['old_type'], t['new_namespace'])
                if t['old_type'].endswith('Attribute'):
                    self.new_ns.setdefault(t['old_type'][:-9], t['new_namespace'])
        self.name_ns = tbl.name_ns(self.done)

    def quals(self, old):
        segs = self.new_ns[old].split('.')
        return ['global::' + self.new_ns[old] + '.'] + ['.'.join(segs[i:]) + '.' for i in range(len(segs))]


def code_events(ctx, src):
    ev = []
    for m in IDENT_RE.finditer(src.masked):
        w = m.group(0)
        if w in ctx.dm:
            ev.append((m.start(), m.end(), 'code', ctx.dm[w], w))
    for start, val in doc_cref_values(src):
        for m in IDENT_RE.finditer(val):
            if m.group(0) in ctx.dm:
                ev.append((start + m.start(), start + m.end(), 'cref', ctx.dm[m.group(0)], m.group(0)))
    return ev


def pieces_of(text, cut_names, rep, path):
    """{piece name: [(a, b) segments of the body]}: '' is the source file, then one piece per cut type (header + block). Offsets in the body."""
    bom, body = A.split_bom(text)
    head_end = len(L.split_head(body)[0])
    blocks = []
    for name in cut_names:
        try:
            s, e = A.block_of(text, name)
        except A.R2Error as ex:
            rep.bad('%s: the block of %s cannot be found: %s' % (path, name, ex))
            return None, head_end
        blocks.append((s - len(bom), e - len(bom), name))
    blocks.sort()
    for (s1, e1, n1), (s2, e2, n2) in zip(blocks, blocks[1:]):
        if e1 > s2:
            rep.bad('%s: the blocks of %s and %s overlap' % (path, n1, n2))
    if blocks and blocks[0][0] < head_end:
        rep.bad('%s: the block of %s starts inside the header' % (path, blocks[0][2]))
    segs, pos = [], 0
    for s, e, _ in blocks:
        segs.append((pos, s))
        pos = e
    segs.append((pos, len(body)))
    pieces = {'': segs}
    for s, e, name in blocks:
        pieces[name] = [(0, head_end), (s, e)]
    return pieces, head_end


def local_events(evs, segs, path, rep):
    """The events that lie in the segments, with offsets local to the concatenated segments: [(index, s, e, kind, new, old)]."""
    out, base = [], 0
    for a, b in segs:
        for i, ev in enumerate(evs):
            s, e = ev[0], ev[1]
            if a <= s and e <= b:
                out.append((i, base + s - a, base + e - a) + tuple(ev[2:]))
            elif s < b and e > a and not (e <= a or s >= b):
                rep.bad('%s: an event at offset %d straddles a cut boundary' % (path, s))
        base += b - a
    return out


def walk(ctx, path, bbody, abody, events, rep):
    """The body of after against the body of before and its events [(s, e, kind, new, old)] (sorted, non-overlapping)."""
    pa = pb = 0
    used_q = 0
    for s, e, kind, new, old in events:
        seg = bbody[pb:s]
        if abody[pa:pa + len(seg)] != seg:
            d = first_diff(abody[pa:], seg)
            rep.bad('%s:%d: unexpected change near %r (expected %r)' % (path, line_of(abody, pa + d), abody[pa + d:pa + d + 40], seg[d:d + 40]))
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
                rep.bad('%s:%d: `%s` is not rewritten as `%s` (alone or qualified by its namespace): %r' % (path, line_of(bbody, s), old, new, abody[pa:pa + 50]))
                return
            rep.count('code occurrences' if kind == 'code' else 'cref occurrences')
        else:
            if not abody.startswith(new, pa):
                rep.bad('%s:%d: the %s entry `%s` is not rewritten as `%s`: %r' % (path, line_of(bbody, s), kind, bbody[s:e], new, abody[pa:pa + 50]))
                return
            pa += len(new)
            rep.count({'lit': 'literal entries', 'qual': 'qualified entries', 'cap': 'capture entries'}.get(kind, kind))
        pb = e
    if abody[pa:] != bbody[pb:]:
        d = first_diff(abody[pa:], bbody[pb:])
        rep.bad('%s:%d: unexpected change near %r (expected %r)' % (path, line_of(abody, pa + d), abody[pa + d:pa + d + 40], bbody[pb + d:pb + d + 40]))
        return
    rep.count('qualifications', used_q)


def check_head(ctx, path, bhead, hevents, ahead, after_body, rep):
    """The head of after: the expected head (events applied) plus `using Alundra.Tests...;` lines justified by the file."""
    exp = L.apply_events(bhead, [(s, e, k, n) for s, e, k, n, o in hevents])
    exp_lines, aft_lines = exp.split('\n'), ahead.split('\n')
    i, added = 0, []
    for l in aft_lines:
        if i < len(exp_lines) and l == exp_lines[i]:
            i += 1
            continue
        m = USING_ADD_RE.match(l)
        if m:
            added.append(m.group(1))
            continue
        rep.bad('%s:%d: the head differs from the expected one: %r (expected %r)' % (path, aft_lines.index(l) + 1, l[:100], exp_lines[i][:100] if i < len(exp_lines) else '<end>'))
        return
    if i != len(exp_lines):
        rep.bad('%s: a line of the head is missing: %r' % (path, exp_lines[i][:100]))
        return
    if added:
        own = A.file_namespace(ahead + '\n')
        enclosing = {'.'.join((own or '').split('.')[:k]) for k in range(1, len((own or '').split('.')) + 1)}
        names = A.Src(after_body).names()
        needed = {ctx.name_ns[n] for n in names if n in ctx.name_ns and ctx.name_ns[n] not in enclosing}
        for ns in added:
            if ns not in needed:
                rep.bad('%s: `using %s;` is added but the file names no test type of that namespace' % (path, ns))
            rep.count('using lines added')


def prove_code(tbl, domain, before, after, rep, only):
    ctx = Ctx(tbl, domain, only)
    done = ctx.done
    # which files exist before, which are new, which vanish
    exp_new = {c['new_path'] for c in tbl.cuts if c['domain'] == domain}
    added, removed = set(after) - set(before), set(before) - set(after)
    if added != exp_new:
        rep.bad('code %s: the new files differ from the table: missing %s, unexpected %s' % (domain, sorted(exp_new - added)[:5], sorted(added - exp_new)[:5]))
    if removed:
        rep.bad('code %s: files removed: %s' % (domain, sorted(removed)[:5]))
    outside = [p for p in set(before) | set(after) if not p.startswith('Alundra.Tests/')]
    for p in sorted(outside):
        if before.get(p) != after.get(p):
            rep.bad('%s: a file outside Alundra.Tests changed' % p)
    # the table of the files before
    mapped = {}
    for r in tbl.files:
        if r['kind'] == 'move':
            mapped[tbl.path_now(r, done)] = r
        elif r['kind'] == 'cut' and r['domain'] in done and r['domain'] != domain:
            mapped[r['new_path']] = None
    for p in sorted(p for p in before if p.startswith('Alundra.Tests/')):
        if p not in mapped:
            rep.bad('%s is not a file of the tables at this point of the slice' % p)
    for p, r in sorted(mapped.items()):
        if p not in before:
            rep.bad('%s should exist before this commit (domains done: %s)' % (p, domain))
    # the entries of the domain belong to existing files
    for e in tbl.literals + tbl.qualified:
        if e['domain'] == domain and tbl.by_old.get(e['file'], {}).get('kind') != 'move':
            rep.bad('the survey entry of %s names a file the table does not know' % e['file'])
    # every file
    for p, r in sorted(mapped.items()):
        if p not in before:
            continue
        text = before[p]
        bom, body = A.split_bom(text)
        try:
            src = A.Src(body)
            evs = code_events(ctx, src)
            if r is not None:   # r is None for a new file of an earlier domain: only the renames and the usings touch it
                evs += [(s, e, k, n, '') for s, e, k, n in L.entry_events(tbl, r['old_path'], src, domain)]
                if r['domain'] == domain:
                    evs.append(L.namespace_event(src, r['namespace']) + ('',))
        except L.R5Error as ex:
            rep.bad('%s: %s' % (p, ex))
            continue
        evs.sort()
        for (s1, e1, *_), (s2, e2, *_) in zip(evs, evs[1:]):
            if e1 > s2:
                rep.bad('%s: two events overlap near offset %d' % (p, s2))
        cut_names = [] if r is None else [c['main_type'] for c in tbl.cuts if c['source_file'] == r['old_path'] and c['domain'] == domain]
        pieces, head_end = pieces_of(text, cut_names, rep, p)
        if pieces is None:
            continue
        consumed = set()
        for name, segs in pieces.items():
            target = p if name == '' else next(c['new_path'] for c in tbl.cuts if c['source_file'] == r['old_path'] and c['main_type'] == name and c['domain'] == domain)
            btext = bom + ''.join(body[a:b] for a, b in segs)
            lev = local_events(evs, segs, p, rep)
            consumed.update(x[0] for x in lev)
            check_piece(ctx, target, btext, name, [x[1:] for x in lev], text, after.get(target), r, name, head_end, rep)
        for i in range(len(evs)):
            if i not in consumed:
                rep.bad('%s: an event at offset %d falls in no piece' % (p, evs[i][0]))
        rep.count('files checked')


def check_piece(ctx, path, btext, name, lev, parent_text, atext, row, piece, head_end, rep):
    """One file of after (a source or a new file) against its expected before."""
    if atext is None:
        rep.bad('%s: absent after the commit' % path)
        return
    bbom, bbody = A.split_bom(btext)
    abom, abody = A.split_bom(atext)
    if bbom != abom:
        rep.bad('%s: the BOM changed' % path)
        return
    try:
        bhead, brest = L.split_head(bbody)
        ahead, arest = L.split_head(abody)
    except L.R5Error as ex:
        rep.bad('%s: %s' % (path, ex))
        return
    he = len(bhead)
    hev = [(s, e, k, n, o) for (s, e, k, n, o) in lev if e <= he]
    rev = [(s - he, e - he, k, n, o) for (s, e, k, n, o) in lev if s >= he]
    if len(hev) + len(rev) != len(lev):
        rep.bad('%s: an event straddles the end of the header' % path)
        return
    walk(ctx, path, brest, arest, rev, rep)
    check_head(ctx, path, bhead, hev, ahead, abody, rep)


# ---------------------------------------------------------------------------------------------------- main
def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument('--domain', required=True)
    ap.add_argument('--kind', choices=('move', 'code'), required=True)
    ap.add_argument('--commit', default='HEAD')
    ap.add_argument('--index', action='store_true')
    ap.add_argument('--worktree', action='store_true')
    ap.add_argument('--before-dir')
    ap.add_argument('--after-dir')
    ap.add_argument('--only-domain', action='store_true')
    ap.add_argument('--tables', default=HERE)
    ap.add_argument('--repo')
    a = ap.parse_args()
    sys.stdout.reconfigure(encoding='utf-8')
    if a.domain not in L.DOMAINS:
        ap.error('a domain of %s' % L.DOMAINS)
    if a.repo:
        A.REPO = L.REPO = os.path.abspath(a.repo)
    rep = Report()
    try:
        tbl = L.R5(a.tables)
        if a.kind == 'move':
            ch = diff_raw(None, None) if a.index else diff_raw(a.commit + '^', a.commit)
            prove_move(tbl, a.domain, ch, rep)
        else:
            if a.before_dir and a.after_dir:
                before, after = L.read_dir_tree(a.before_dir), L.read_dir_tree(a.after_dir)
            elif a.worktree:
                before, after = L.read_rev('HEAD'), L.read_dir_tree(L.REPO)
                for st, p in worktree_changed():
                    if not (p.startswith('Alundra.Tests/') and p.endswith('.cs')) and not p.startswith(('CasaEngineMonogame', 'alundra-datas-analyser', 'tools/dll-structure/', 'tools/Convert-', 'tools/Saves/', 'docs/plan-restructuration-dll.md')):
                        rep.bad('%s: a path outside the slice changed (%s)' % (p, st))
            else:
                before, after = L.read_rev(a.commit + '^'), L.read_rev(a.commit)
                for st, p, _, _ in diff_raw(a.commit + '^', a.commit):
                    if not (p.startswith('Alundra.Tests/') and p.endswith('.cs')):
                        rep.bad('%s: a path outside the .cs files of Alundra.Tests changed (%s)' % (p, st))
            prove_code(tbl, a.domain, before, after, rep, a.only_domain)
    except (L.R5Error, A.R2Error) as e:
        print('ERROR: %s' % e)
        return 2
    for p in rep.problems:
        print(p)
    st = ', '.join('%d %s' % (v, k) for k, v in sorted(rep.stats.items()))
    print('prove_r5: %s %s (%s) -> %s' % (a.kind, a.domain, st, 'OK' if not rep.problems else '%d violation(s)' % len(rep.problems)))
    return 0 if not rep.problems else 1


if __name__ == '__main__':
    sys.exit(main())
