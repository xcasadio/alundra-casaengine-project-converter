"""Proof of the R2 commits (docs/plan-restructuration-dll.md, section 6, T0 (c)). Read-only on the repository.

  python -B tools/dll-structure/prove_r2.py --domain <Domain> --kind move [--commit REV]
  python -B tools/dll-structure/prove_r2.py --domain <Domain> --kind fix  [--commit REV]
  python -B tools/dll-structure/prove_r2.py --all --base REV     every commit of REV..HEAD, then the end state of the tree

REV defaults to HEAD (compared with its parent). Files are paired by table.json (never by the rename detection of git).

kind move (step 1): the commit holds nothing but the moves of the domain: deleted old paths and added new paths are exactly those of the
table for the domain, and each added blob is the very blob of the deleted file (a 100 % rename).
kind fix (step 2): the added files are exactly the split types whose target is in the domain; for each, the block (everything after its
`namespace` line) is found byte for byte in the file it comes from, and absent from that file after the commit; the new file declares that
one type, in the namespace of the `r2` stage, with a header made of `using`/`#nullable`/comment/blank lines. Then, for every existing file
changed (the source files taken without their cut blocks), every changed line is one of: the removal of `using Alundra.Scripts;`, the
addition of a `using Alundra...;` that the file justifies (it names a type of that namespace, or calls an extension method of it), the
`namespace` line of a DLL file of the domain (never of a test), a `Alundra.Scripts.<T>` qualification (code or cref, never in a string)
rewritten to the namespace of a type that has moved, or one of the source paths `"Alundra", "Scripts", "<File>.cs"` of a test.
With --all: every commit of the range is proven by its subject (`move the X files`, `put the X domain`); a commit that touches
Alundra/, Alundra.Tests/ or tools/AlundraTestSaves/ under another subject fails; at the end no `Alundra.Scripts.<T>` qualification or cref
remains (code and cref) for a type that has moved, and the tree has its `r2` namespaces.
Exit 0 when everything holds, 1 otherwise (every violation is listed).
"""
import argparse
import difflib
import os
import re
import subprocess
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import apply_r2 as A  # noqa: E402  (the table, the order, the lexer)
import scan_types  # noqa: E402

REPO = A.REPO
QUAL_RE = A.QUAL_RE
USING_ALUNDRA_RE = re.compile(r'^using (Alundra(?:\.\w+)*);$')


def gitb(*args):
    r = subprocess.run(['git', '-C', REPO, *args], capture_output=True)
    if r.returncode != 0:
        raise A.R2Error('git %s: %s' % (' '.join(args), r.stderr.decode('utf-8', 'replace').strip()))
    return r.stdout


def show(rev, path):
    return gitb('show', '%s:%s' % (rev, path)).decode('latin-1')


def diff_tree(parent, rev):
    """[(status, path, old_sha, new_sha)] without rename detection."""
    raw = gitb('diff-tree', '-r', '--no-renames', '-z', parent, rev).split(b'\0')
    out, i = [], 0
    if raw and raw[0] and not raw[0].startswith(b':'):
        i = 1  # the commit id line, if any
    while i < len(raw) and raw[i]:
        meta = raw[i].decode()
        path = raw[i + 1].decode('utf-8')
        f = meta[1:].split()
        out.append((f[4][0], path, f[2], f[3]))
        i += 2
    return out


def in_tranche(p):
    return any(p.startswith(r) for r in A.TRANCHE_ROOTS)


class Report:
    def __init__(self):
        self.problems = []

    def bad(self, msg):
        self.problems.append(msg)


# ---------------------------------------------------------------------------------------------------- move
def prove_move(tb, domain, rev, rep):
    parent = rev + '^'
    ch = diff_tree(parent, rev)
    expected = {o: n for o, n in tb.file_moves().items() if A.Table.domain(n) == domain}
    dele = {p: s for st, p, s, _ in ch if st == 'D'}
    add = {p: s for st, p, _, s in ch if st == 'A'}
    other = [(st, p) for st, p, _, _ in ch if st not in 'DA']
    if other:
        rep.bad('move %s: files modified (only renames are allowed): %s' % (domain, other[:5]))
    if set(dele) != set(expected):
        rep.bad('move %s: deleted paths differ from the table: missing %s, unexpected %s' % (
            domain, sorted(set(expected) - set(dele))[:5], sorted(set(dele) - set(expected))[:5]))
    if set(add) != set(expected.values()):
        rep.bad('move %s: added paths differ from the table: missing %s, unexpected %s' % (
            domain, sorted(set(expected.values()) - set(add))[:5], sorted(set(add) - set(expected.values()))[:5]))
    for o, n in expected.items():
        if o in dele and n in add and dele[o] != add[n]:
            rep.bad('move %s: %s -> %s is not a 100%% rename' % (domain, o, n))
    return len(expected)


# ---------------------------------------------------------------------------------------------------- fix
HEADER_OK_RE = re.compile(r'^(?:using[ \t]+[^\n]*;|#nullable[^\n]*|//[^\n]*|[ \t]*)$')


def ext_namespaces(tb, done, rev):
    """{method name: namespace} of the extension methods of the DLL at `rev`."""
    out = {}
    files = [p for p in gitb('ls-tree', '-r', '--name-only', rev, '--', 'Alundra').decode('utf-8').splitlines()
             if p.endswith('.cs') and not A.EXCLUDED_DIR_RE.search(p)]
    for p in files:
        t = show(rev, p)
        body = A.split_bom(t)[1]
        if 'this ' not in body:
            continue
        src = A.Src(body)
        if not A.EXT_RE.search(src.masked):
            continue
        ext = A.top_level_extents(src)
        for m in A.EXT_RE.finditer(src.masked):
            cls = [n for n, (a, b) in ext.items() if a <= m.start() < b]
            rows = tb.by_name.get(cls[0]) if cls else None
            if rows:
                out[m.group(1)] = tb.cur_ns(rows[0], done)
    return out


def code_position_ok(old_line, idx):
    """Heuristic on one line: is the occurrence at idx in code (even quote count, no // before) or inside a cref attribute?"""
    stripped = old_line.lstrip()
    if stripped.startswith('///') or stripped.startswith('//'):
        k = old_line.rfind('cref="', 0, idx)
        return k >= 0 and '"' not in old_line[k + 6:idx]
    before = old_line[:idx]
    if '//' in before or '/*' in before:
        k = before.rfind('cref="')
        return k >= 0 and '"' not in before[k + 6:]
    return before.replace('\\"', '').count('"') % 2 == 0


def qual_pair(old, new, name_ns):
    pos_o = pos_n = 0
    changed = False
    for m in QUAL_RE.finditer(old):
        chunk = old[pos_o:m.start()]
        if not new.startswith(chunk, pos_n):
            return False
        pos_n += len(chunk)
        ns = name_ns.get(m.group(1))
        rewritten = (ns + '.' + m.group(1)) if ns and ns != A.SCRIPTS_NS else None
        if rewritten and new.startswith(rewritten, pos_n) and code_position_ok(old, m.start()):
            pos_n += len(rewritten)
            changed = True
        elif new.startswith(m.group(0), pos_n):
            pos_n += len(m.group(0))
        else:
            return False
        pos_o = m.end()
    return changed and new[pos_n:] == old[pos_o:]


def prove_fix(tb, domain, rev, rep):
    parent = rev + '^'
    done = tb.done_set(domain)
    name_ns = tb.name_ns(done)
    ch = diff_tree(parent, rev)
    for st, p, _, _ in ch:
        if not in_tranche(p) or not p.endswith('.cs'):
            rep.bad('fix %s: a file outside the .cs files of the slice changed: %s' % (domain, p))
        if st == 'D':
            rep.bad('fix %s: a file is deleted: %s' % (domain, p))
    added = {p for st, p, _, _ in ch if st == 'A'}
    modified = [p for st, p, _, _ in ch if st == 'M']
    cuts = [r for r in tb.rows if r['split'] and A.Table.domain(r['r2']['file']) == domain]
    expected_new = {r['r2']['file'] for r in cuts}
    if added != expected_new:
        rep.bad('fix %s: added files differ from the split types of the domain: missing %s, unexpected %s' % (
            domain, sorted(expected_new - added)[:5], sorted(added - expected_new)[:5]))
    # the cut blocks
    base_text = {}   # source path -> text before the commit, minus the blocks cut so far
    for r in sorted(cuts, key=lambda x: (x['old_file'], x['old_name'])):
        new_path = r['r2']['file']
        if new_path not in added:
            continue
        src_path = tb.cur_file(r, done)
        n_text = show(rev, new_path)
        bom, body = A.split_bom(n_text)
        m = A.NS_LINE_RE.search(body)
        if not m:
            rep.bad('fix %s: %s has no namespace line' % (domain, new_path))
            continue
        if m.group(2) != r['r2']['namespace']:
            rep.bad('fix %s: %s declares namespace %s, the table says %s' % (domain, new_path, m.group(2), r['r2']['namespace']))
        eol = body.find('\n', m.start())
        header, block = body[:m.start()], body[eol + 1:]
        for line in header.split('\n'):
            if not HEADER_OK_RE.match(line.rstrip('\r')):
                rep.bad('fix %s: %s: unexpected line in the header: %r' % (domain, new_path, line[:60]))
        decls = [d for d in scan_types.parse(new_path, body) if not d['nested']]
        if [d['name'] for d in decls] != [r['old_name']]:
            rep.bad('fix %s: %s declares %s, expected only %s' % (domain, new_path, [d['name'] for d in decls], r['old_name']))
        if not block.startswith(('\n', '\r\n')) or not block.endswith('\n'):
            rep.bad('fix %s: %s: the block does not start with a blank line and end with a line end' % (domain, new_path))
        if src_path not in base_text:
            try:
                base_text[src_path] = show(parent, src_path)
            except A.R2Error:
                rep.bad('fix %s: the source %s of %s does not exist in the parent' % (domain, src_path, r['old_name']))
                continue
        P = base_text[src_path]
        bp = A.split_bom(P)[1]
        i = bp.find(block)
        if i < 0:
            rep.bad('fix %s: the block of %s is not found byte for byte in %s' % (domain, r['old_name'], src_path))
            continue
        if bp.count(block) != 1:
            rep.bad('fix %s: the block of %s occurs %d times in %s' % (domain, r['old_name'], bp.count(block), src_path))
        if i > 0 and bp[i - 1] != '\n':
            rep.bad('fix %s: the block of %s does not start at a line start in %s' % (domain, r['old_name'], src_path))
        base_text[src_path] = A.split_bom(P)[0] + bp[:i] + bp[i + len(block):]
        C = show(rev, src_path)
        if block in A.split_bom(C)[1]:
            rep.bad('fix %s: the block of %s is still in %s after the commit' % (domain, r['old_name'], src_path))
    # the lines changed in the existing files
    ext_ns = None
    ns_expected = {m['r2']['file']: m['r2']['namespace'] for m in tb.main.values() if A.Table.domain(m['r2']['file']) == domain}
    valid_ns = {r['r2']['namespace'] for r in tb.rows} | {A.SCRIPTS_NS}
    for p in sorted(set(modified) | {s for s in base_text}):
        a_text = base_text.get(p)
        if a_text is None:
            a_text = show(parent, p)
        b_text = show(rev, p)
        a_lines = A.split_bom(a_text)[1].split('\n')
        b_lines = A.split_bom(b_text)[1].split('\n')
        is_dll = p.startswith('Alundra/')
        added_usings, blank_adds, rm_scripts = [], 0, 0
        sm = difflib.SequenceMatcher(None, a_lines, b_lines, autojunk=False)
        for tag, i1, i2, j1, j2 in sm.get_opcodes():
            if tag == 'equal':
                continue
            rem, add = [], []
            for l in a_lines[i1:i2]:
                if l.strip() == 'using Alundra.Scripts;':
                    rm_scripts += 1
                else:
                    rem.append(l)
            for l in b_lines[j1:j2]:
                mu = USING_ALUNDRA_RE.match(l.strip())
                if mu and mu.group(1) in valid_ns:
                    added_usings.append(mu.group(1))
                elif l.strip() == '':
                    blank_adds += 1
                else:
                    add.append(l)
            if len(rem) != len(add):
                rep.bad('fix %s: %s: lines %d-%d changed in an unexplained way (%d removed, %d added)' % (domain, p, i1 + 1, i2, len(rem), len(add)))
                continue
            for o, n in zip(rem, add):
                o, n = o.rstrip('\r'), n.rstrip('\r')
                om, nm_ = re.match(r'^namespace ([\w.]+);$', o), re.match(r'^namespace ([\w.]+);$', n)
                if om and nm_:
                    if not (is_dll and p in ns_expected and nm_.group(1) == ns_expected[p]):
                        rep.bad('fix %s: %s: unexpected namespace change %s -> %s' % (domain, p, om.group(1), nm_.group(1)))
                    continue
                if qual_pair(o, n, name_ns):
                    continue
                if 'Alundra' in o and A.PATH_RE.search(o) and not is_dll and A.rewrite_paths(o, tb, done, True) == n:
                    continue
                rep.bad('fix %s: %s: line %d changed outside the allowed classes: %r -> %r' % (domain, p, i1 + 1, o.strip()[:80], n.strip()[:80]))
        if blank_adds and (not added_usings or blank_adds > 1):
            rep.bad('fix %s: %s: blank lines added without using lines' % (domain, p))
        # the justification of the using lines
        if added_usings or rm_scripts:
            src = A.Src(A.split_bom(b_text)[1])
            names = src.names()
            used = {name_ns[n] for n in names if n in name_ns}
            for u in added_usings:
                if u in used:
                    continue
                if ext_ns is None:
                    ext_ns = ext_namespaces(tb, done, rev)
                if any(ext_ns.get(mm.group(1)) == u for mm in re.finditer(r'\.\s*(\w+)\s*(?:<[^()]*>)?\s*\(', src.masked)):
                    continue
                rep.bad('fix %s: %s: using %s is not justified (no type of that namespace is named)' % (domain, p, u))
            if rm_scripts and A.SCRIPTS_NS in used and A.file_namespace(b_text) != A.SCRIPTS_NS:
                rep.bad('fix %s: %s: using Alundra.Scripts removed but a type still in Alundra.Scripts is named' % (domain, p))
    # the namespaces of the files of the domain
    for p, ns in ns_expected.items():
        try:
            got = A.file_namespace(show(rev, p))
        except A.R2Error:
            rep.bad('fix %s: %s is missing' % (domain, p))
            continue
        if got != ns:
            rep.bad('fix %s: %s declares namespace %s, expected %s' % (domain, p, got, ns))
    return len(expected_new)


# ---------------------------------------------------------------------------------------------------- the whole range
def leftover_qualifications(tb, rep):
    done = set(A.ORDER)
    name_ns = tb.name_ns(done)
    out = gitb('ls-files', '--', *[r.rstrip('/') for r in A.TRANCHE_ROOTS]).decode('utf-8').splitlines()
    n = 0
    for p in out:
        if not p.endswith('.cs') or A.EXCLUDED_DIR_RE.search(p) or not os.path.isfile(os.path.join(REPO, p)):
            continue
        src = A.Src(A.split_bom(A.read(p))[1])
        hits = [(m.start(), m.group(1)) for m in QUAL_RE.finditer(src.masked)]
        for start, val in src.cref_values():
            hits += [(start + m.start(), m.group(1)) for m in QUAL_RE.finditer(val)]
        for off, t in hits:
            ns = name_ns.get(t)
            if ns and ns != A.SCRIPTS_NS:
                line = src.text.count('\n', 0, off) + 1
                rep.bad('end state: %s:%d: Alundra.Scripts.%s remains, the type is in %s' % (p, line, t, ns))
                n += 1
    return n


def prove_all(tb, base, rep):
    revs = gitb('rev-list', '--reverse', '%s..HEAD' % base).decode().split()
    nmove = nfix = 0
    for rev in revs:
        subject = gitb('log', '-1', '--format=%s', rev).decode('utf-8', 'replace').strip()
        mv = re.match(r'^refactor\(dll\): move the (\w+) files', subject)
        fx = re.match(r'^refactor\(dll\): put the (\w+) domain into', subject)
        if mv and mv.group(1) in A.ORDER:
            prove_move(tb, mv.group(1), rev, rep)
            nmove += 1
        elif fx and fx.group(1) in A.ORDER:
            prove_fix(tb, fx.group(1), rev, rep)
            nfix += 1
        else:
            touched = [p for st, p, _, _ in diff_tree(rev + '^', rev) if in_tranche(p)]
            if touched:
                rep.bad('commit %s (%s) touches %s under another subject' % (rev[:8], subject[:60], touched[:3]))
    leftover_qualifications(tb, rep)
    return nmove, nfix


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument('--domain')
    ap.add_argument('--kind', choices=('move', 'fix'))
    ap.add_argument('--commit', default='HEAD')
    ap.add_argument('--all', action='store_true')
    ap.add_argument('--base')
    a = ap.parse_args()
    sys.stdout.reconfigure(encoding='utf-8')
    rep = Report()
    try:
        tb = A.Table()
        if a.all:
            if not a.base:
                ap.error('--all needs --base')
            nm, nf = prove_all(tb, a.base, rep)
            what = '%d move commit(s), %d fix commit(s), end state' % (nm, nf)
        else:
            if a.domain not in A.ORDER or not a.kind:
                ap.error('--domain <one of %s> and --kind' % A.ORDER)
            n = prove_move(tb, a.domain, a.commit, rep) if a.kind == 'move' else prove_fix(tb, a.domain, a.commit, rep)
            what = '%s %s: %d file(s)' % (a.kind, a.domain, n)
    except A.R2Error as e:
        print('ERROR: %s' % e)
        return 2
    for p in rep.problems:
        print(p)
    print('prove_r2: %s -> %s' % (what, 'OK' if not rep.problems else '%d violation(s)' % len(rep.problems)))
    return 0 if not rep.problems else 1


if __name__ == '__main__':
    sys.exit(main())
