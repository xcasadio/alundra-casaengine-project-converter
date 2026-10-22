"""R5 library (docs/plan-restructuration-dll.md, section 9): the tables of the slice and what apply_r5.py, prove_r5.py and the discovery share.
Python 3, standard library only. Read-only: it never writes.

The tables (all in tools/dll-structure/, versed by T0a, values of BEFORE R5):
  r5-files.tsv       one row per file of Alundra.Tests after R5 (kind `move`: an existing file at old_path, `cut`: a new file made by the cut of
                     P-R5-1, `keep`: a file that does not move); the domain whose commit moves or creates it
  r5-types.tsv       one row per top-level type: old and new full names, its arrival file, the domain, `cut` (yes:<reason> | no), `main`
  r5-literals.tsv    L1: the string literals that carry an old test class name (file at the pre-R5 path, token, rank of the occurrence, domain)
  r5-qualified.tsv   Q1 and capture: the qualified references that follow a moved test type (file, kind, text, rank, domain, new text)

An entry is located by its OCCURRENCE (file, exact text, rank among the occurrences of that text in the file as the code or the strings
carry it), never by its line number (revision 3 of the plan).
"""
import collections
import csv
import os
import re
import subprocess
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import apply_r2 as A  # noqa: E402  (the lexer, Src, cut_type, block_of, NS_LINE_RE...)

REPO = A.REPO
DOMAINS = ['Support', 'Maps', 'Session', 'Entities', 'Events', 'Ai', 'Hero', 'Combat', 'Items', 'Rng', 'Physics', 'Cells', 'Cameras',
           'Rendering', 'Audio', 'Transitions', 'Dialogues', 'UI', 'SaveGames', 'Probes', 'ExportGuards', 'Arcs']
TESTS = 'Alundra.Tests/'
ROOTS = ('Alundra', 'Alundra.Tests', 'tools/AlundraTestSaves')
DATA_FILE = 'Alundra.Tests/Data/story-chain-skipped-opcodes.tsv'
DATA_LITERAL_FILE = 'Alundra.Tests/AlundraStoryChainOpcodeAudit.cs'


class R5Error(Exception):
    pass


def read_tsv(path):
    with open(path, encoding='utf-8', newline='') as f:
        return list(csv.DictReader(f, delimiter='\t'))


def split_head(body):
    """(head, rest) of a source body (BOM already removed): the head ends after the end of the `namespace X;` line."""
    m = A.NS_LINE_RE.search(body)
    if not m:
        raise R5Error('no file-scoped namespace line')
    nl = body.find('\n', m.end())
    cut = len(body) if nl < 0 else nl + 1
    return body[:cut], body[cut:]


class R5:
    """The tables, indexed."""

    def __init__(self, dirpath=HERE):
        j = lambda n: os.path.join(dirpath, n)
        self.files = read_tsv(j('r5-files.tsv'))
        self.types = read_tsv(j('r5-types.tsv'))
        self.literals = read_tsv(j('r5-literals.tsv')) if os.path.exists(j('r5-literals.tsv')) else []
        self.qualified = read_tsv(j('r5-qualified.tsv')) if os.path.exists(j('r5-qualified.tsv')) else []
        self.by_old = {r['old_path']: r for r in self.files if r['old_path']}
        self.by_new = {r['new_path']: r for r in self.files}
        self.cs = [r for r in self.files if r['kind'] in ('move', 'cut')]
        self.cuts = [r for r in self.files if r['kind'] == 'cut']
        self.type_by_old_full = collections.defaultdict(list)
        for t in self.types:
            self.type_by_old_full[t['old_full']].append(t)
        self.old_namespaces = {t['old_namespace'] for t in self.types}
        self.old_test_names = {t['old_type'] for t in self.types if t['old_type'] != t['new_type']}
        self.lit_by_file = collections.defaultdict(list)
        for e in self.literals:
            self.lit_by_file[e['file']].append(e)
        self.qual_by_file = collections.defaultdict(list)
        for e in self.qualified:
            self.qual_by_file[e['file']].append(e)

    # ------------------------------------------------------------------------------------------ domains
    @staticmethod
    def done(domain, only=False):
        i = DOMAINS.index(domain)
        return {domain} if only else set(DOMAINS[:i + 1])

    def path_now(self, row, done):
        """The path of a file of the table once the domains `done` are complete (step 1 included)."""
        return row['new_path'] if row['domain'] in done else row['old_path']

    def dm(self, domain):
        """{old simple name: new simple name} of the types renamed in `domain` (the short form of an attribute class is added)."""
        out = {}
        for t in self.types:
            if t['domain'] == domain and t['old_type'] != t['new_type']:
                out[t['old_type']] = t['new_type']
        return out

    def name_ns(self, done, unrenamed=None):
        """{simple name as written now: current namespace} of every test type once the domains `done` are complete. `unrenamed` is a domain
        whose types are already in their new namespace but still carry their old names (between the namespace step and the rename)."""
        res = {}
        for t in self.types:
            if t['domain'] == unrenamed:
                name, ns = t['old_type'], t['new_namespace']
            elif t['domain'] in done:
                name, ns = t['new_type'], t['new_namespace']
            else:
                name, ns = t['old_type'], t['old_namespace']
            if res.get(name, ns) != ns:
                raise R5Error('the test type name %s has two current namespaces: %s and %s' % (name, res[name], ns))
            res[name] = ns
        return res


# ---------------------------------------------------------------------------------------------------- git and files
def git(*args, check=True):
    r = subprocess.run(['git', '-C', REPO, *args], capture_output=True)
    if check and r.returncode != 0:
        raise R5Error('git %s failed: %s' % (' '.join(args), r.stderr.decode('utf-8', 'replace').strip()))
    return r.stdout


def in_roots(p):
    return p.endswith('.cs') and any(p.startswith(r + '/') for r in ROOTS) and not A.EXCLUDED_DIR_RE.search(p)


def read_rev(rev, roots=ROOTS):
    """{path: text (latin-1)} of the .cs files of the roots at a revision (as git stores them: LF)."""
    out = git('ls-tree', '-r', '-z', rev, '--', *roots).split(b'\0')
    items = []
    for ent in out:
        if not ent:
            continue
        meta, path = ent.split(b'\t', 1)
        p = path.decode('utf-8')
        if in_roots(p):
            items.append((p, meta.split()[2].decode()))
    data = subprocess.run(['git', '-C', REPO, 'cat-file', '--batch'], input=''.join(s + '\n' for _, s in items).encode(), capture_output=True).stdout
    res, pos = {}, 0
    for p, sha in items:
        nl = data.index(b'\n', pos)
        size = int(data[pos:nl].split()[2])
        res[p] = data[nl + 1:nl + 1 + size].decode('latin-1')
        pos = nl + 1 + size + 1
    return res


def read_blob(rev, path):
    return git('show', '%s:%s' % (rev, path)).decode('latin-1')


def read_dir_tree(root):
    """{path: text} of the .cs files of the roots in a directory (working tree or a copy), CRLF folded to LF like git."""
    res = {}
    for r in ROOTS:
        base = os.path.join(root, r)
        for dp, dn, fn in os.walk(base):
            dn[:] = [d for d in dn if d not in ('bin', 'obj', 'artifacts', 'TestResults')]
            for f in fn:
                if f.endswith('.cs'):
                    full = os.path.join(dp, f)
                    rel = os.path.relpath(full, root).replace(os.sep, '/')
                    if in_roots(rel):
                        with open(full, 'rb') as h:
                            res[rel] = h.read().decode('latin-1').replace('\r\n', '\n')
    return res


# ---------------------------------------------------------------------------------------------------- the occurrences
LIT_TOK = re.compile(r'(?<![A-Za-z0-9_])(Alundra[A-Za-z0-9]*)')
QCHAIN = re.compile(r'(?<![\w.@:])Alundra[ \t]*\.[ \t]*Tests\b(?:[ \t]*\.[ \t]*[A-Za-z_]\w*)*')
CAP = re.compile(r'(?<![\w.@:])([A-Za-z_]\w*)[ \t]*\.[ \t]*([A-Za-z_]\w*)')


def literal_occurrences(src, old_names):
    """[(start, end, token, rank)] of the old test class names inside string literals (regular, verbatim, raw, interpolated pieces)."""
    out, rank = [], collections.Counter()
    for a, b, k in src.spans:
        if k != 's' or src.text.startswith("'", a):
            continue
        for m in LIT_TOK.finditer(src.text, a, b):
            tok = m.group(1)
            if tok in old_names:
                rank[tok] += 1
                out.append((m.start(1), m.end(1), tok, rank[tok]))
    return out


def qualified_occurrences(src, tbl):
    """[(start, end_of_ns_part, end_of_chain, kind, text, rank, ns_tokens, type_full)] of the `Alundra.Tests...` chains of the code.
    kind `type` (text = ns + '.' + type, the member that follows is not part of it), `namespace` (a whole `using X;` line), or `unknown`."""
    out, rank = [], collections.Counter()
    m_ = src.masked
    for m in QCHAIN.finditer(m_):
        ls = m_.rfind('\n', 0, m.start()) + 1
        before = m_[ls:m.start()].strip()
        if before.startswith('namespace'):
            continue
        raw = m.group(0)
        spaced = bool(re.search(r'[ \t]', raw))
        toks = re.sub(r'[ \t]', '', raw).split('.')
        kind, ns_end_tok, type_full = 'unknown', None, None
        for i in range(2, len(toks)):
            cand = '.'.join(toks[:i + 1])
            if cand in tbl.type_by_old_full:
                kind, ns_end_tok, type_full = 'type', i, cand
                break
        if kind == 'unknown' and '.'.join(toks) in tbl.old_namespaces and before == 'using' and m_[m.end():m.end() + 1] in (';', ' '):
            kind = 'namespace'
        if kind == 'type':
            text = '.'.join(toks[:ns_end_tok + 1])
            # the end offset of the namespace part in the source text (tokens may be spaced)
            pos = m.start()
            tok_ends = []
            for t in toks:
                s = m_.index(t, pos)
                pos = s + len(t)
                tok_ends.append(pos)
            end_ns = tok_ends[ns_end_tok - 1]
        else:
            text, end_ns = '.'.join(toks), m.end()
        rank[(kind, text)] += 1
        out.append(dict(start=m.start(), end_ns=end_ns, end=m.end(), kind=kind, text=text, rank=rank[(kind, text)], spaced=spaced, type_full=type_full))
    return out


def capture_occurrences(src, texts):
    """[(start, end, text, rank)] of the `Seg.Name` chains (first two tokens of a chain) whose text is in `texts`."""
    out, rank = [], collections.Counter()
    for m in CAP.finditer(src.masked):
        t = m.group(1) + '.' + m.group(2)
        if t in texts:
            rank[t] += 1
            out.append((m.start(), m.end(), t, rank[t]))
    return out


# ---------------------------------------------------------------------------------------------------- the events of a file
def entry_events(tbl, key, src, domain):
    """The events of the survey entries of `domain` for the file whose pre-R5 path is `key`, located in `src` (the text of that file now):
    [(start, end, kind, new_text)] with kind lit | qual | cap | drop (the whole line). Raises R5Error when an entry has no occurrence."""
    evs = []
    lit = [e for e in tbl.lit_by_file.get(key, []) if e['domain'] == domain]
    if lit:
        occ = {(t, r): (a, b) for a, b, t, r in literal_occurrences(src, tbl.old_test_names)}
        for e in lit:
            k = (e['old'], int(e['rank']))
            if k not in occ:
                raise R5Error('%s: the literal entry %s #%s is not found' % (key, e['old'], e['rank']))
            evs.append((occ[k][0], occ[k][1], 'lit', e['new']))
    ql = [e for e in tbl.qual_by_file.get(key, []) if e['domain'] == domain]
    if ql:
        qocc = {(o['kind'], o['text'], o['rank']): o for o in qualified_occurrences(src, tbl)}
        cap_texts = {e['text'] for e in ql if e['kind'] == 'capture'}
        cocc = {(t, r): (a, b) for a, b, t, r in capture_occurrences(src, cap_texts)} if cap_texts else {}
        for e in ql:
            if e['kind'] == 'capture':
                k = (e['text'], int(e['rank']))
                if k not in cocc:
                    raise R5Error('%s: the capture entry %s #%s is not found' % (key, e['text'], e['rank']))
                evs.append((cocc[k][0], cocc[k][1], 'cap', e['new_text']))
            else:
                k = ('type' if e['kind'] in ('static-using', 'inline') else 'namespace', e['text'], int(e['rank']))
                if k not in qocc:
                    raise R5Error('%s: the qualified entry %s #%s is not found' % (key, e['text'], e['rank']))
                o = qocc[k]
                if o['kind'] == 'namespace':
                    ls = src.text.rfind('\n', 0, o['start']) + 1
                    le = src.text.find('\n', o['end'])
                    evs.append((ls, (len(src.text) if le < 0 else le + 1), 'drop', ''))
                else:
                    evs.append((o['start'], o['end_ns'], 'qual', e['new_text']))
    return evs


def apply_events(text, evs):
    """text with the events applied (non-overlapping; applied from the end)."""
    prev_start = len(text) + 1
    for s, e, kind, new in sorted(evs, reverse=True):
        if e > prev_start:
            raise R5Error('overlapping events near offset %d' % s)
        text = text[:s] + new + text[e:]
        prev_start = s
    return text


def namespace_event(src, ns):
    m = A.NS_LINE_RE.search(src.masked)
    if not m:
        raise R5Error('no namespace line')
    return (m.start(2), m.end(2), 'ns', ns)


# ---------------------------------------------------------------------------------------------------- the checks of the tables
def check_tables(tbl):
    """The checks of T0a on the versed tables against the tracked files (HEAD index + working tree): list of problems."""
    import scan_types
    import check_names
    bad = []
    tracked = [p for p in git('ls-files', '--', 'Alundra.Tests').decode('utf-8').splitlines() if not A.EXCLUDED_DIR_RE.search(p) and '/TestResults/' not in p]
    old_rows = collections.Counter(r['old_path'] for r in tbl.files if r['old_path'])
    for p in tracked:
        if old_rows[p] != 1:
            bad.append('%s is covered %d time(s) by r5-files.tsv' % (p, old_rows[p]))
    for p in old_rows:
        if p not in tracked:
            bad.append('r5-files.tsv names %s which is not a tracked file' % p)
    lower = collections.Counter(r['new_path'].lower() for r in tbl.files)
    for p, c in lower.items():
        if c > 1:
            bad.append('the new path %s is taken %d times (case-insensitive)' % (p, c))
    olds = {r['old_path'] for r in tbl.files if r['old_path']}
    for r in tbl.files:
        if r['kind'] == 'move' and r['new_path'] in olds and r['new_path'] != r['old_path']:
            bad.append('the new path %s of %s is the old path of another file' % (r['new_path'], r['old_path']))
        if r['kind'] == 'keep' and r['new_path'] != r['old_path']:
            bad.append('%s is kept but would move' % r['old_path'])
        if r['kind'] in ('move', 'cut'):
            folder = r['new_path'][len(TESTS):].rsplit('/', 1)[0]
            if r['namespace'] != 'Alundra.Tests.' + folder.replace('/', '.'):
                bad.append('%s: namespace %s is not the folder %s' % (r['new_path'], r['namespace'], folder))
            if r['domain'] not in DOMAINS or r['domain'] != folder.split('/')[0]:
                bad.append('%s: domain %s is not the first folder %s' % (r['new_path'], r['domain'], folder))
            if 'Alundra' in r['new_path'].rsplit('/', 1)[1][:7]:
                bad.append('%s: the file name keeps the prefix' % r['new_path'])
    # the declared types, once
    declared = {}
    for p in tracked:
        if p.endswith('.cs'):
            with open(os.path.join(REPO, p), 'rb') as f:
                text = f.read().decode('utf-8-sig', errors='replace')
            for d in scan_types.parse(p, text):
                if not d['nested']:
                    declared.setdefault((p, d['ns'] + '.' + d['name']), d)
    rows = collections.Counter((t['old_file'], t['old_full']) for t in tbl.types)
    for k in declared:
        if rows[k] != 1:
            bad.append('%s: type %s is covered %d time(s) by r5-types.tsv' % (k[0], k[1], rows[k]))
    for k in rows:
        if k not in declared:
            bad.append('r5-types.tsv names %s in %s which is not declared there' % (k[1], k[0]))
    # the arrival file of each type exists in r5-files.tsv in the same namespace; one main type per file; cuts
    new_files = {r['new_path']: r for r in tbl.files}
    per_file = collections.defaultdict(list)
    for t in tbl.types:
        per_file[t['new_file']].append(t)
        r = new_files.get(t['new_file'])
        if r is None:
            bad.append('the arrival file %s of %s is not in r5-files.tsv' % (t['new_file'], t['old_full']))
        elif r['namespace'] != t['new_namespace'] or r['domain'] != t['domain']:
            bad.append('%s: namespace or domain differs from its file %s' % (t['old_full'], t['new_file']))
        if t['new_type'].startswith('Alundra') or t['new_type'].startswith('IAlundra'):
            bad.append('%s keeps the prefix' % t['new_full'])
    for path, ts in per_file.items():
        mains = [t for t in ts if t['main'] == 'yes']
        stem = path.rsplit('/', 1)[1][:-3].split('.')[0]
        if len(mains) > 1 or (len(mains) == 1 and mains[0]['new_type'] != stem):
            bad.append('%s: its main type is %s' % (path, [t['new_type'] for t in mains]))
        if len(mains) == 0 and new_files[path]['kind'] != 'cut':
            bad.append('%s has no main type' % path)
        for t in ts:
            if t['main'] != 'yes' and t['cut'] == 'no' and t['access'] == 'public':
                bad.append('%s: the public secondary type %s is not cut' % (path, t['old_type']))
    for r in tbl.cuts:
        src = tbl.by_old.get(r['source_file'])
        if src is None or src['new_path'].rsplit('/', 1)[0] != r['new_path'].rsplit('/', 1)[0]:
            bad.append('%s: not in the folder of its source %s' % (r['new_path'], r['source_file']))
        if not any(t['old_file'] == r['source_file'] and t['old_type'] == r['main_type'] and t['cut'].startswith('yes') for t in tbl.types):
            bad.append('%s: its type is not marked as cut in r5-types.tsv' % r['new_path'])
    # the collisions of the names (check_names.collision_findings) and the segments
    ext = scan_types.external_types()
    items = [('r5', t['new_namespace'], t['new_type']) for t in tbl.types]
    dll = [(d['path'], d['ns'], d['name']) for d in scan_types.dll_decls()[0] if not d['nested']]
    for line in check_names.collision_findings(items + dll, ext):
        bad.append(line)
    dup = collections.Counter((t['new_namespace'], t['new_type']) for t in tbl.types)
    parts = {(t['new_namespace'], t['new_type']) for t in tbl.types if t['partial'] == 'True'}
    for k, c in dup.items():
        if c > 1 and k not in parts:
            bad.append('the type %s.%s is declared %d times after R5' % (k[0], k[1], c))
    return bad


def main(argv=None):
    import argparse
    ap = argparse.ArgumentParser(description='R5 tables: the checks of T0a.')
    ap.add_argument('--check-tables', action='store_true')
    a = ap.parse_args(argv)
    sys.stdout.reconfigure(encoding='utf-8')
    if not a.check_tables:
        ap.error('--check-tables')
    tbl = R5()
    bad = check_tables(tbl)
    print('r5 tables: %d files, %d new files, %d types, %d literals, %d qualified entries' % (len(tbl.files), len(tbl.cuts), len(tbl.types), len(tbl.literals), len(tbl.qualified)))
    for b in bad:
        print(b)
    print('%d problem(s)' % len(bad))
    return 1 if bad else 0


if __name__ == '__main__':
    sys.exit(main())
