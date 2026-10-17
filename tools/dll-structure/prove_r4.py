"""Proof of the R4 commits (docs/plan-restructuration-dll.md, section 8, T0 (b)). Python 3, standard library only. Read-only.

  python -B tools/dll-structure/prove_r4.py --kind move [--commit REV | --index]
  python -B tools/dll-structure/prove_r4.py --kind code [--commit REV | --index]
  python -B tools/dll-structure/prove_r4.py --kind end

REV defaults to HEAD (compared with its parent); --index proves the staged tree against HEAD (what the next commit would be). Files are paired by
path; table.json gives the 10 files that move, the 9 names that change and the namespaces that change.

kind move (T1): the commit holds nothing but the 10 renames (`Alundra/Scripts/<File>.cs` -> the `final` path of the table), each at 100 % (the very blob).
kind code (T2): no file is added or deleted; the changed files are only
  - .cs files of Alundra/, Alundra.Tests/ and tools/AlundraTestSaves/, each checked OCCURRENCE BY OCCURRENCE after a C# lexer has separated the code,
    the strings, the characters and the comments. The `using` lines apart (below), the expected after is rebuilt from the before and compared to the byte:
      * code (`cref` and `nameof` included): an old name of the 9 types becomes its final name, preceded by nothing or by one qualification (the final
        namespace, or `global::` and the final namespace); a `Alundra.Scripts.<T>` qualification of a moved type becomes `<final namespace>.<T>`;
      * strings: an occurrence of an old name changes only if r4-literals.tsv has an entry of class 1 or 1b for the file, line and name, exactly into the
        final name; a class 4 entry stays; an occurrence the survey does not list is a refusal; all the rest of the literal stays;
      * the `namespace` line of the 10 files: `Alundra.Scripts` -> the final namespace of the table; no other `namespace` line changes;
      * comments (cref excepted), characters and every other byte: unchanged;
    `using` lines: the only lines removed are `using Alundra.Scripts;`; the only lines added are `using <ns>;` for the namespace of a moved type that the
    file names (and is neither its own nor an enclosing one), plus one blank line when the file had no `using` at all;
  - the six converter files (three writers, three tests) where the old full names `Alundra.Scripts.<Old>` become the final full names (one line each, or a
    comment line that names `Scripts`), and the six `UI/Screens/*.design.json` where `view_model_type` takes the final full name (the file is otherwise
    unchanged); every one of these twelve files must change;
  - nothing else.
kind end (T3): no `Alundra.Scripts` left in the C# files of the three roots (code, cref, using), none in the six converter files and the six design.json,
  `Alundra/Scripts/` is absent, the 10 files are at their final path under their final namespace, r4-literals.tsv holds in the tree (after).
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
import apply_r4 as P  # noqa: E402
import r3_literals as L  # noqa: E402
from prove_r2 import diff_tree, show  # noqa: E402
from prove_r3 import read_rev, read_dir, decode, line_of, first_diff, doc_cref_values, in_roots, gitb  # noqa: E402

REPO = A.REPO
IDENT_RE = re.compile(r'[A-Za-z_]\w*')
QUAL = 'Alundra.Scripts.'
CONVERTER_FILES = [
    'alundra-casaengine-project-converter/Writers/WorldWriter.cs',
    'alundra-casaengine-project-converter/Writers/SpriteWriter.cs',
    'alundra-casaengine-project-converter/Writers/PlayerSetupWriter.cs',
    'alundra-casaengine-project-converter.Tests/WorldWriterTests.cs',
    'alundra-casaengine-project-converter.Tests/PlayerSetupWriterTests.cs',
    'alundra-casaengine-project-converter.Tests/SpriteWriterBodyPrefabTests.cs',
]
SCREENS = {  # design.json -> old view model (r3 name)
    'alundra-project/UI/Screens/ChoiceScreen.design.json': 'AlundraChoiceViewModel',
    'alundra-project/UI/Screens/HudScreen.design.json': 'AlundraHudViewModel',
    'alundra-project/UI/Screens/InventoryScreen.design.json': 'AlundraInventoryViewModel',
    'alundra-project/UI/Screens/SaveScreen.design.json': 'AlundraSaveScreenViewModel',
    'alundra-project/UI/Screens/SubInventoryScreen.design.json': 'AlundraSubInventoryViewModel',
    'alundra-project/UI/Screens/TextBoxScreen.design.json': 'AlundraTextBoxViewModel',
}


class Report:
    def __init__(self):
        self.problems = []
        self.stats = {}

    def bad(self, msg):
        self.problems.append(msg)

    def count(self, key, n=1):
        self.stats[key] = self.stats.get(key, 0) + n


class Ctx:
    def __init__(self):
        self.r4 = P.R4()
        self.entries = L.entries_by_key(L.load_tsv(P.TSV))
        self.all_old = self.r4.all_old
        self.moved_tokens = set(self.r4.old_names) | {'MapEvent'}   # names a code token may carry before the commit

    def final_of(self, tok):
        return self.r4.rename.get(tok, tok)

    def final_full(self, tok):
        return self.r4.moved_ns[self.final_of(tok)] + '.' + self.final_of(tok)


# ---------------------------------------------------------------------------------------------------- move
def prove_move(ctx, parent, rev, rep):
    ch = diff_tree(parent, rev)
    expected = ctx.r4.moves
    dele = {p: s for st, p, s, _ in ch if st == 'D'}
    add = {p: s for st, p, _, s in ch if st == 'A'}
    other = [(st, p) for st, p, _, _ in ch if st not in 'DA']
    if other:
        rep.bad('move: files modified (only renames are allowed): %s' % other[:5])
    if set(dele) != set(expected):
        rep.bad('move: deleted paths differ from the table: missing %s, unexpected %s' % (
            sorted(set(expected) - set(dele))[:5], sorted(set(dele) - set(expected))[:5]))
    if set(add) != set(expected.values()):
        rep.bad('move: added paths differ from the table: missing %s, unexpected %s' % (
            sorted(set(expected.values()) - set(add))[:5], sorted(set(add) - set(expected.values()))[:5]))
    for o, n in expected.items():
        if o in dele and n in add and dele[o] != add[n]:
            rep.bad('move: %s -> %s is not a 100%% rename' % (o, n))
    rep.count('files moved', len(expected))


# ---------------------------------------------------------------------------------------------------- code
def split_head(lines):
    """(index of the first `namespace` line or None, {using name: index} before it)."""
    ns_idx, usings = None, {}
    for i, l in enumerate(lines):
        if re.match(r'^namespace[ \t]+[\w.]+[ \t]*;', l):
            ns_idx = i
            break
        m = A.USING_RE.match(l.rstrip('\r'))
        if m:
            usings[m.group(1)] = i
    return ns_idx, usings


def strip_usings(path, btext, atext, rep):
    """(before without `using Alundra.Scripts;`, after without the added `using` lines, removed set, added set, number of lines dropped from the before)."""
    bl, al = btext.split('\n'), atext.split('\n')
    bn, bu = split_head(bl)
    an, au = split_head(al)
    if bn is None or an is None:
        return btext, atext, set(), set(), 0
    removed = set(bu) - set(au)
    added = set(au) - set(bu)
    for u in removed:
        if u != 'Alundra.Scripts':
            rep.bad('%s: the using line `%s` is removed' % (path, u))
    drop_b = {bu[u] for u in removed}
    drop_a = {au[u] for u in added}
    if added and not bu:
        # a file without any `using` gets one blank line after the added lines
        last = max(drop_a)
        if last + 1 < len(al) and al[last + 1].strip() == '':
            drop_a.add(last + 1)
    return ('\n'.join(l for i, l in enumerate(bl) if i not in drop_b),
            '\n'.join(l for i, l in enumerate(al) if i not in drop_a), removed, added, len(drop_b))


def check_namespace(ctx, path, bb, ab, rep):
    """The namespace line of the 10 files; returns both texts with the value replaced by a placeholder when the path is one of them."""
    if path not in ctx.r4.file_ns:
        return bb, ab
    bn, an = A.file_namespace(bb), A.file_namespace(ab)
    if bn != A.SCRIPTS_NS:
        rep.bad('%s: the namespace was %s before the commit, expected Alundra.Scripts' % (path, bn))
    if an != ctx.r4.file_ns[path]:
        rep.bad('%s: declares namespace %s after the commit, the table says %s' % (path, an, ctx.r4.file_ns[path]))
    return A.set_namespace(bb, '@NS@'), A.set_namespace(ab, '@NS@')


def walk(ctx, path, bbody, abody, shift, rep):
    """Occurrence by occurrence: bbody (before, usings aside) rebuilt into abody (after, usings aside)."""
    r4 = ctx.r4
    src = A.Src(bbody)
    events = []   # (start, end, kind, candidates, old text)

    def add_token(start, end, text, idx, tok, kind):
        final = ctx.final_of(tok)
        full = ctx.final_full(tok)
        if text.endswith(QUAL, 0, idx):
            events.append((start - len(QUAL), end, kind + '-q', [full], tok))
        else:
            ns = r4.moved_ns[final]
            events.append((start, end, kind, ['global::' + ns + '.' + final, ns + '.' + final, final], tok))

    for m in IDENT_RE.finditer(src.masked):
        if m.group(0) in ctx.moved_tokens:
            add_token(m.start(), m.end(), src.masked, m.start(), m.group(0), 'code')
    for start, val in doc_cref_values(src):
        for m in IDENT_RE.finditer(val):
            if m.group(0) in ctx.moved_tokens:
                add_token(start + m.start(), start + m.end(), val, m.start(), m.group(0), 'cref')
    canon = r4.canon(path)
    rx = P.name_rx(ctx.all_old)
    for a, b, k in src.spans:
        if k != 's' or bbody.startswith("'", a):
            continue
        line = line_of(bbody, a) + shift   # the survey counts the lines of the file as it was (the dropped `using` lines are in the head)
        for m in rx.finditer(bbody, a, b):
            w = m.group(1)
            e = ctx.entries.get((canon, line, w))
            if e is None:
                rep.bad('%s:%d: a literal names %s and the survey does not list it' % (path, line, w))
                continue
            if e['class'] in P.APPLIED:
                events.append((m.start(1), m.end(1), 'lit', [r4.rename[w]], w))
                rep.count('literal occurrences')
            else:
                rep.count('literal occurrences kept')
    events.sort()
    pa = pb = 0
    for s, e, kind, cands, old in events:
        if s < pb:
            rep.bad('%s:%d: overlapping occurrences of %s' % (path, line_of(bbody, s), old))
            return
        seg = bbody[pb:s]
        if abody[pa:pa + len(seg)] != seg:
            d = first_diff(abody[pa:], seg)
            rep.bad('%s:%d: unexpected change near %r (expected %r)' % (path, line_of(abody, pa + d), abody[pa + d:pa + d + 30], seg[d:d + 30]))
            return
        pa += len(seg)
        for c in cands:
            if abody.startswith(c, pa):
                pa += len(c)
                break
        else:
            rep.bad('%s:%d: `%s` is not rewritten as one of %s: %r' % (path, line_of(bbody, s), bbody[s:e], cands, abody[pa:pa + 50]))
            return
        rep.count({'code': 'code occurrences', 'cref': 'cref occurrences', 'code-q': 'qualifications', 'cref-q': 'cref qualifications', 'lit': 'literal rewrites'}[kind])
        pb = e
    if abody[pa:] != bbody[pb:]:
        d = first_diff(abody[pa:], bbody[pb:])
        rep.bad('%s:%d: unexpected change near %r (expected %r)' % (path, line_of(abody, pa + d), abody[pa + d:pa + d + 30], bbody[pb + d:pb + d + 30]))


def ext_namespaces(ctx, rev):
    out = {}
    for p, n in ctx.r4.file_ns.items():
        try:
            t = show(rev, p)
        except A.R2Error:
            continue
        body = A.split_bom(t)[1]
        if 'this ' not in body:
            continue
        src = A.Src(body)
        ext = A.top_level_extents(src)
        for m in A.EXT_RE.finditer(src.masked):
            if [x for x, (a, b) in ext.items() if a <= m.start() < b]:
                out[m.group(1)] = n
    return out


def prove_cs(ctx, path, before_bytes, after_bytes, ext_ns, rep):
    bb, ab = decode(before_bytes), decode(after_bytes)
    if bb[0] != ab[0]:
        rep.bad('%s: the BOM changed' % path)
        return
    btext, atext = bb[1], ab[1]
    bt, at, removed, added, shift = strip_usings(path, btext, atext, rep)
    bt, at = check_namespace(ctx, path, bt, at, rep)
    walk(ctx, path, bt, at, shift, rep)
    # the justification of the using lines
    own = A.file_namespace(atext)
    enclosing = A.own_namespaces(own)
    names = A.Src(atext).names()
    needed = {ctx.r4.moved_ns[n] for n in names if n in ctx.r4.moved_ns}
    masked = A.Src(atext).masked
    for mm in re.finditer(r'\.\s*(\w+)\s*(?:<[^()]*>)?\s*\(', masked):
        ns = ext_ns.get(mm.group(1))
        if ns:
            needed.add(ns)
    needed -= enclosing
    for u in sorted(added):
        if u not in needed:
            rep.bad('%s: using %s is not justified (no moved type of that namespace is named)' % (path, u))
    present = set(split_head(atext.split('\n'))[1])
    for u in sorted(needed - present):
        rep.bad('%s: using %s is missing (a moved type of that namespace is named)' % (path, u))
    if A.SCRIPTS_NS in present:
        rep.bad('%s: `using Alundra.Scripts;` remains' % path)
    rep.count('using lines added', len(added))
    rep.count('using lines removed', len(removed))
    rep.count('files changed')


FULL_RE = re.compile(r'(?<![\w.])Alundra\.Scripts\.(\w+)')


def prove_converter(ctx, path, btext, atext, rep):
    """One line at a time: the old full names become the final full names, or a comment line that names Scripts changes."""
    bl, al = btext.split('\n'), atext.split('\n')
    if len(bl) != len(al):
        rep.bad('%s: the number of lines changed (%d -> %d)' % (path, len(bl), len(al)))
        return
    hits = 0
    for i, (o, n) in enumerate(zip(bl, al)):
        if o == n:
            continue
        exp = FULL_RE.sub(lambda m: ctx.final_full(m.group(1)) if m.group(1) in ctx.r4.old_names else m.group(0), o)
        if exp == n and exp != o:
            hits += 1
            continue
        if o.lstrip().startswith('//') and n.lstrip().startswith('//') and 'Scripts' in o:
            rep.count('converter comment lines')
            continue
        rep.bad('%s:%d: a line changed outside the full names: %r -> %r' % (path, i + 1, o.strip()[:90], n.strip()[:90]))
    if hits == 0:
        rep.bad('%s: no old full name `Alundra.Scripts.<T>` replaced by the final full name' % path)
    rep.count('converter full names', hits)


def prove_screen(ctx, path, btext, atext, rep):
    old = SCREENS[path]
    o = '"view_model_type": "%s%s"' % (QUAL, old)
    n = '"view_model_type": "%s"' % ctx.final_full(old)
    if btext.count(o) != 1:
        rep.bad('%s: %s not found once before the commit' % (path, o))
    elif btext.replace(o, n) != atext:
        rep.bad('%s: the file differs from the before in more than `view_model_type` -> %s' % (path, ctx.final_full(old)))
    else:
        rep.count('design.json')


def prove_code(ctx, parent, rev, rep):
    ch = diff_tree(parent, rev)
    cs, other_ok, seen = [], set(CONVERTER_FILES) | set(SCREENS), set()
    for st, p, _, _ in ch:
        if st != 'M':
            rep.bad('code: a file is %s: %s' % ('added' if st == 'A' else 'deleted', p))
            continue
        if p.endswith('.cs') and in_roots(p):
            cs.append(p)
        elif p in other_ok:
            seen.add(p)
        else:
            rep.bad('code: a file outside the slice changed: %s' % p)
    for p in sorted(other_ok - seen):
        rep.bad('code: %s did not change (the named change of the slice is missing)' % p)
    if not cs:
        rep.bad('code: no .cs file changed')
    before, after = read_rev(parent), read_rev(rev)
    ext_ns = ext_namespaces(ctx, rev)
    for p in sorted(cs):
        if p not in before or p not in after:
            rep.bad('code: %s is missing on one side' % p)
            continue
        prove_cs(ctx, p, before[p], after[p], ext_ns, rep)
    for p in sorted(seen):
        bt, at = show(parent, p), show(rev, p)
        if p in SCREENS:
            prove_screen(ctx, p, bt, at, rep)
        else:
            prove_converter(ctx, p, bt, at, rep)
    for o, n in ctx.r4.file_ns.items():
        if n and A.file_namespace(after.get(o, b'').decode('latin-1')) != n:
            rep.bad('code: %s does not declare %s' % (o, n))


# ---------------------------------------------------------------------------------------------------- end state
def prove_end(ctx, rep):
    tree = read_dir(REPO)
    for p, b in tree.items():
        body = decode(b)[1]
        src = A.Src(body)
        for m in re.finditer(r'\bAlundra\.Scripts\b', src.masked):
            rep.bad('end: %s:%d: `Alundra.Scripts` remains in code' % (p, line_of(body, m.start())))
        for start, val in doc_cref_values(src):
            if 'Alundra.Scripts' in val:
                rep.bad('end: %s:%d: `Alundra.Scripts` remains in a cref' % (p, line_of(body, start)))
    if os.path.isdir(os.path.join(REPO, 'Alundra', 'Scripts')):
        rep.bad('end: Alundra/Scripts/ still exists')
    for o, n in ctx.r4.moves.items():
        if os.path.exists(os.path.join(REPO, o)):
            rep.bad('end: %s still exists' % o)
        if not os.path.isfile(os.path.join(REPO, n)):
            rep.bad('end: %s is missing' % n)
        elif A.file_namespace(A.read(n)) != ctx.r4.file_ns[n]:
            rep.bad('end: %s does not declare %s' % (n, ctx.r4.file_ns[n]))
    for p in CONVERTER_FILES + list(SCREENS):
        with open(os.path.join(REPO, p), 'rb') as f:
            t = f.read().decode('utf-8', 'replace')
        for m in re.finditer(r'Alundra\.Scripts\.\w+', t):
            rep.bad('end: %s: `%s` remains' % (p, m.group(0)))
    for pr in P.check(ctx.r4, REPO, 'after'):
        rep.bad('end: ' + pr)


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument('--kind', choices=('move', 'code', 'end'), required=True)
    ap.add_argument('--commit', default='HEAD')
    ap.add_argument('--index', action='store_true')
    a = ap.parse_args()
    sys.stdout.reconfigure(encoding='utf-8')
    rep = Report()
    try:
        ctx = Ctx()
        if a.kind == 'end':
            prove_end(ctx, rep)
            what = 'end state'
        else:
            if a.index:
                rev, parent = gitb('write-tree').decode().strip(), 'HEAD'
                what = '%s (staged tree)' % a.kind
            else:
                rev, parent = a.commit, a.commit + '^'
                what = '%s %s' % (a.kind, a.commit)
            (prove_move if a.kind == 'move' else prove_code)(ctx, parent, rev, rep)
    except A.R2Error as e:
        print('ERROR: %s' % e)
        return 2
    for p in rep.problems:
        print(p)
    st = ', '.join('%d %s' % (v, k) for k, v in sorted(rep.stats.items()))
    print('prove_r4: %s (%s) -> %s' % (what, st, 'OK' if not rep.problems else '%d violation(s)' % len(rep.problems)))
    return 0 if not rep.problems else 1


if __name__ == '__main__':
    sys.exit(main())
