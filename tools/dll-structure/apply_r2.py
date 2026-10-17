"""R2 of the DLL restructuring (docs/plan-restructuration-dll.md, section 6, T0 (b)): moves and splits, domain by domain, no rename.

  python -B tools/dll-structure/apply_r2.py <Domain> --move   step 1: `git mv` of the files of the domain to their `r2` path of table.json
                                                              (not one byte changes; the code of before compiles as it is)
  python -B tools/dll-structure/apply_r2.py <Domain> --fix    step 2: (a) the `namespace` line of the files of the domain; (b) the cut of the
                                                              split types whose target is in the domain; (c) for ALL .cs files of the DLL, of
                                                              Alundra.Tests and of tools/AlundraTestSaves: the `using` lines, the
                                                              `Alundra.Scripts.<T>` qualifications and the three source paths of a test
  python -B tools/dll-structure/apply_r2.py --domains         the domains, in order

Domains (section 2 order): see ORDER. Nothing is committed (the executor commits by path); new files are `git add`ed.

Rules of step 2 (section 6, T0 (b)):
  - the cut copies the declaration with its documentation comments, attributes and ONE leading blank line, byte for byte, into a new file
    named after the type, with the header of the file where the type is at that moment (its `using` lines, its `#nullable`, its comments)
    and the `namespace` line of the `r2` stage; the block is removed from the file it comes from;
  - the `using` lines are computed from the CURRENT location of each type of the table (after the moves and cuts already done: this
    domain and the previous ones): a file gets `using <ns>;` for every namespace of a type it names (code without comments or strings,
    `cref` included) and of every extension class of the DLL whose method it calls, except its own namespace and the namespaces that
    enclose it; added after the existing `using` lines, in alphabetical order; `using Alundra.Scripts;` stays while the file names a type
    that is still in `Alundra.Scripts`, and is removed otherwise;
  - a `Alundra.Scripts.<T>` qualification (code and `cref`) and one of the source paths `"Alundra", "Scripts", "<File>.cs"` are rewritten
    only when the type or the file has moved, to its namespace or its path of the moment;
  - no `global using`; the script refuses to run when the tree is not clean outside the paths of the slice (and inside them, unless
    `--allow-dirty`).
The files keep their line endings and their encoding byte for byte (they are read and written as latin-1), but for the lines changed.
"""
import argparse
import bisect
import collections
import json
import os
import re
import subprocess
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.abspath(os.path.join(HERE, '..', '..'))
TABLE = os.path.join(HERE, 'table.json')

ORDER = ['Maps', 'Session', 'Entities', 'Events', 'Ai', 'Hero', 'Combat', 'Items', 'Rng', 'Physics', 'Cells', 'Cameras', 'Rendering',
         'Audio', 'Transitions', 'Dialogues', 'UI', 'SaveGames', 'Probes', 'Properties']
SCRIPTS_NS = 'Alundra.Scripts'
TRANCHE_ROOTS = ('Alundra/', 'Alundra.Tests/', 'tools/AlundraTestSaves/')
TRANCHE_ALLOWED = TRANCHE_ROOTS + ('tools/dll-structure/', 'docs/plan-restructuration-dll.md')
SUBMODULES = ('CasaEngineMonogame', 'alundra-datas-analyser')
EXCLUDED_DIR_RE = re.compile(r'(^|/)(bin|obj|artifacts)/')


class R2Error(Exception):
    pass


# ---------------------------------------------------------------------------------------------------- the lexer
# Comments and string literals of C# (regular, verbatim, interpolated with its holes, raw) and character literals. The result is a list of
# spans (start, end, kind) with kind 'c' (comment) or 's' (string or character literal); the code inside the holes of an interpolated string
# stays code. Everything is length-preserving, so offsets in the masked text are offsets in the text.
_STR_BODY = r'(?:\\(?:u[0-9a-fA-F]{4}|x[0-9a-fA-F]{1,4}|U[0-9a-fA-F]{8}|.)|[^\'\\\n])'
_TOKEN_SRC = (r'(?P<lc>//[^\n]*)|(?P<bc>/\*.*?\*/)|(?P<raw>\$*"{3,})|(?P<ivs>\$@"|@\$")|(?P<vs>@")|(?P<is>\$")|(?P<s>")|'
              r"(?P<ch>'" + _STR_BODY + r"')")
TOKEN = re.compile(_TOKEN_SRC, re.S)
TOKEN_H = re.compile(_TOKEN_SRC + r'|(?P<ob>\{)|(?P<cb>\})', re.S)
REG_STR = re.compile(r'"(?:\\.|[^"\\\n])*"?')
VERB_STR = re.compile(r'@"(?:[^"]|"")*"?')


def _handle(m, text, spans):
    """Handle one string/comment token; return the index after it."""
    k = m.lastgroup
    a = m.start()
    if k in ('lc', 'bc', 'ch'):
        spans.append((a, m.end(), 'c' if k != 'ch' else 's'))
        return m.end()
    if k == 's':
        mm = REG_STR.match(text, a)
        spans.append((a, mm.end(), 's'))
        return mm.end()
    if k == 'vs':
        mm = VERB_STR.match(text, a)
        spans.append((a, mm.end(), 's'))
        return mm.end()
    if k == 'raw':
        tok = m.group(0)
        q = tok.count('"')
        end = text.find('"' * q, m.end())
        end = len(text) if end < 0 else end + q
        spans.append((a, end, 's'))
        return end
    # interpolated: '$"' (regular) or '$@"' / '@$"' (verbatim)
    verbatim = k == 'ivs'
    j = m.end()
    piece = a
    n = len(text)
    while j < n:
        c = text[j]
        if verbatim:
            if c == '"':
                if text[j + 1:j + 2] == '"':
                    j += 2
                    continue
                j += 1
                break
        else:
            if c == '\\':
                j += 2
                continue
            if c == '"':
                j += 1
                break
            if c == '\n':
                break
        if c == '{':
            if text[j + 1:j + 2] == '{':
                j += 2
                continue
            spans.append((piece, j, 's'))
            j = _lex_hole(text, j + 1, spans)
            piece = j
            continue
        j += 1
    spans.append((piece, j, 's'))
    return j


def _lex_hole(text, i, spans):
    depth = 1
    while True:
        m = TOKEN_H.search(text, i)
        if not m:
            return len(text)
        k = m.lastgroup
        if k == 'ob':
            depth += 1
            i = m.end()
        elif k == 'cb':
            depth -= 1
            i = m.end()
            if depth == 0:
                return i
        else:
            i = _handle(m, text, spans)


def lex(text):
    spans = []
    i = 0
    while True:
        m = TOKEN.search(text, i)
        if not m:
            break
        i = _handle(m, text, spans)
    spans.sort()
    return spans


_BLANK_RE = re.compile(r'[^\r\n]')


def mask_text(text, spans):
    """The text with comments and string literals blanked (newlines kept)."""
    out, pos = [], 0
    for a, b, _ in spans:
        if a < pos:
            continue
        out.append(text[pos:a])
        out.append(_BLANK_RE.sub(' ', text[a:b]))
        pos = b
    out.append(text[pos:])
    return ''.join(out)


CREF_RE = re.compile(r'cref="([^"]*)"')
IDENT_RE = re.compile(r'[A-Za-z_]\w*')


class Src:
    """One source file: text (latin-1), lexed once."""

    def __init__(self, text):
        self.text = text
        self.spans = lex(text)
        self.masked = mask_text(text, self.spans)
        self._cref = None

    def cref_values(self):
        """[(start, value)] of the cref attributes inside the comments."""
        if self._cref is None:
            out = []
            for a, b, k in self.spans:
                if k == 'c':
                    for m in CREF_RE.finditer(self.text, a, b):
                        out.append((m.start(1), m.group(1)))
            self._cref = out
        return self._cref

    def names(self):
        s = set(IDENT_RE.findall(self.masked))
        for _, v in self.cref_values():
            s.update(IDENT_RE.findall(v))
        return s


# ---------------------------------------------------------------------------------------------------- the table and the state
class Table:
    def __init__(self, path=TABLE):
        t = json.load(open(path, encoding='utf-8'))
        self.t = t
        self.rows = t['types']
        self.by_name = collections.defaultdict(list)
        self.by_old = collections.defaultdict(list)
        for r in self.rows:
            self.by_name[r['old_name']].append(r)
            self.by_old[r['old_file']].append(r)
        self.main = {}
        for f, rs in self.by_old.items():
            ms = [r for r in rs if r['main']]
            if len(ms) != 1:
                raise R2Error('%s: %d main rows' % (f, len(ms)))
            self.main[f] = ms[0]
        self.roots = t['root_files']

    @staticmethod
    def domain(path):
        """Domain of an r2 path: the first folder under Alundra/, or None for a file kept in Alundra/Scripts or at the root."""
        if path.startswith('Alundra/Scripts/'):
            return None
        parts = path.split('/')
        return parts[1] if len(parts) > 2 else None

    def file_moves(self):
        """{old_path: new_path} for every file that moves (not the cited files, not Plugin.cs)."""
        mv = {}
        for f, m in self.main.items():
            if m['r2']['file'] != f:
                mv[f] = m['r2']['file']
        for r in self.roots:
            if r['r2'] != r['old_file']:
                mv[r['old_file']] = r['r2']
        return mv

    def done_set(self, domain):
        return set(ORDER[:ORDER.index(domain) + 1])

    def cur_ns(self, row, done):
        """Namespace of a type once the domains `done` are complete (moves and cuts)."""
        d = self.domain(row['r2']['file'])
        if d is not None and d in done:
            return row['r2']['namespace']
        if row['split']:
            return self.cur_ns(self.main[row['old_file']], done)
        return SCRIPTS_NS

    def cur_file(self, row, done):
        """File of a TYPE's main file once the domains `done` are complete: the r2 file when its domain is done, else the old one."""
        m = self.main[row['old_file']]
        d = self.domain(m['r2']['file'])
        if d is not None and d in done:
            return m['r2']['file']
        return row['old_file']

    def name_ns(self, done):
        """{simple type name: namespace now}; the partial types share one namespace."""
        res = {}
        for name, rs in self.by_name.items():
            nss = {self.cur_ns(r, done) for r in rs}
            if len(nss) != 1:
                raise R2Error('type %s has several current namespaces: %s' % (name, nss))
            res[name] = nss.pop()
        return res


# ---------------------------------------------------------------------------------------------------- git and files
def git(*args, check=True, input=None):
    r = subprocess.run(['git', '-C', REPO, *args], capture_output=True, input=input)
    if check and r.returncode != 0:
        raise R2Error('git %s failed: %s' % (' '.join(args), r.stderr.decode('utf-8', 'replace').strip()))
    return r.stdout.decode('utf-8', 'replace')


def check_tree_clean(allow_dirty):
    out = git('status', '--porcelain=v1', '--untracked-files=all')
    bad_outside, dirty_inside = [], []
    for line in out.splitlines():
        p = line[3:]
        if ' -> ' in p:
            p = p.split(' -> ')[1]
        p = p.strip('"')
        if p in SUBMODULES or any(p.startswith(s + '/') for s in SUBMODULES):
            continue
        if any(p.startswith(a) or p == a for a in TRANCHE_ROOTS):
            dirty_inside.append(line)
        elif any(p.startswith(a) or p == a for a in TRANCHE_ALLOWED):
            continue
        else:
            bad_outside.append(line)
    if bad_outside:
        raise R2Error('the tree is not clean outside the paths of the slice:\n  ' + '\n  '.join(bad_outside))
    if dirty_inside and not allow_dirty:
        raise R2Error('the paths of the slice are not clean (use --allow-dirty to go on):\n  ' + '\n  '.join(dirty_inside[:20]))


def source_files():
    out = git('ls-files', '--cached', '--others', '--exclude-standard', '--', *[r.rstrip('/') for r in TRANCHE_ROOTS])
    res = []
    for p in out.splitlines():
        if p.endswith('.cs') and not EXCLUDED_DIR_RE.search(p) and os.path.isfile(os.path.join(REPO, p)):
            res.append(p)
    return sorted(set(res))


def read(path):
    with open(os.path.join(REPO, path), 'rb') as f:
        return f.read().decode('latin-1')


def write(path, text):
    full = os.path.join(REPO, path)
    os.makedirs(os.path.dirname(full), exist_ok=True)
    with open(full, 'wb') as f:
        f.write(text.encode('latin-1'))


BOM = '\xef\xbb\xbf'
NS_LINE_RE = re.compile(r'^(namespace[ \t]+)([\w.]+)([ \t]*;)', re.M)


def split_bom(text):
    return (BOM, text[len(BOM):]) if text.startswith(BOM) else ('', text)


def eol_of(text):
    return '\r\n' if '\r\n' in text else '\n'


def file_namespace(text):
    m = NS_LINE_RE.search(split_bom(text)[1])
    return m.group(2) if m else None


# ---------------------------------------------------------------------------------------------------- step 1
def do_move(tb, domain, dry):
    moves = {o: n for o, n in tb.file_moves().items() if Table.domain(n) == domain}
    if not moves:
        print('nothing to move for %s' % domain)
        return 0
    for o, n in sorted(moves.items()):
        if not os.path.isfile(os.path.join(REPO, o)):
            raise R2Error('source file missing: %s' % o)
        if os.path.exists(os.path.join(REPO, n)):
            raise R2Error('target already exists: %s' % n)
    for o, n in sorted(moves.items()):
        print('git mv %s -> %s' % (o, n))
        if not dry:
            os.makedirs(os.path.dirname(os.path.join(REPO, n)), exist_ok=True)
            git('mv', o, n)
    print('%d file(s) moved' % len(moves))
    return 0


# ---------------------------------------------------------------------------------------------------- step 2
def set_namespace(text, ns):
    bom, body = split_bom(text)
    m = NS_LINE_RE.search(body)
    if not m:
        raise R2Error('no namespace line')
    return bom + body[:m.start(2)] + ns + body[m.end(2):]


DECL_RE = re.compile(r'^[ \t]*(?:\[[^\]\n]*\][ \t]*\r?\n?[ \t]*)*(?:(?:public|internal|private|protected|static|sealed|abstract|partial|readonly|ref|file|unsafe|new)\s+)*'
                     r'(?:class|struct|interface|enum|record\s+struct|record\s+class|record)\s+([A-Za-z_]\w*)', re.M)


def top_level_extents(src):
    """{name: (decl_start_line_offset, end_offset_after_eol)} for the top-level declarations (file-scoped namespace: depth 0)."""
    masked = src.masked
    braces = [(m.start(), m.group(0)) for m in re.finditer(r'[{}]', masked)]
    pos, depth, depths = [], 0, []
    for p, c in braces:
        pos.append(p)
        depth += 1 if c == '{' else -1
        depths.append(depth)

    def depth_at(off):
        i = bisect.bisect_left(pos, off)
        return depths[i - 1] if i > 0 else 0

    res = {}
    for m in DECL_RE.finditer(masked):
        name = m.group(1)
        if depth_at(m.start(1)) != 0:
            continue
        # the end: the first `{` or `;` at parenthesis depth 0 after the name
        j, par = m.end(1), 0
        n = len(masked)
        while j < n:
            c = masked[j]
            if c == '(':
                par += 1
            elif c == ')':
                par -= 1
            elif par == 0 and c in '{;':
                break
            j += 1
        if j >= n:
            raise R2Error('unterminated declaration %s' % name)
        if masked[j] == '{':
            d = 0
            while j < n:
                if masked[j] == '{':
                    d += 1
                elif masked[j] == '}':
                    d -= 1
                    if d == 0:
                        break
                j += 1
        end = j + 1
        nl = masked.find('\n', end)
        end = n if nl < 0 else nl + 1
        decl_line_start = masked.rfind('\n', 0, m.start(1)) + 1
        # the declaration may start on an attribute line above: DECL_RE matched from the start of the first attribute
        start = m.start()
        start = masked.rfind('\n', 0, start) + 1
        if name in res:
            raise R2Error('top-level type %s declared twice in a file' % name)
        res[name] = (start, end)
    return res


_ATTACHED_RE = re.compile(r'^[ \t]*(///|//|\[)')


def block_of(text, name):
    """(start, end) of the block of the top-level type `name`: leading blank line, comments, attributes, declaration, to the end of its line."""
    bom, body = split_bom(text)
    src = Src(body)
    ext = top_level_extents(src)
    if name not in ext:
        raise R2Error('type %s not found at the top level' % name)
    start, end = ext[name]
    # attached lines above: documentation comments, comments, attributes
    lines_before = body[:start].split('\n')  # the last element is '' (start is a line start)
    k = len(lines_before) - 1
    off = start
    while k > 0:
        line = lines_before[k - 1]
        if _ATTACHED_RE.match(line):
            off -= len(line) + 1
            k -= 1
        else:
            break
    start = off
    # one leading blank line
    prev_start = body.rfind('\n', 0, start - 1) + 1 if start > 0 else 0
    if start == 0 or body[prev_start:start].strip() != '':
        raise R2Error('type %s: no blank line before its documentation' % name)
    start = prev_start
    # a directive just above would be left behind: not handled
    return len(bom) + start, len(bom) + end


def cut_type(src_text, name, new_ns):
    """(new file text, source text without the block)."""
    start, end = block_of(src_text, name)
    bom, body = split_bom(src_text)
    m = NS_LINE_RE.search(body)
    if not m:
        raise R2Error('no namespace line in the source of %s' % name)
    eol = body.find('\n', m.start())
    header = body[:eol + 1]
    header = header[:m.start(2)] + new_ns + header[m.end(2):]
    block = src_text[start:end]
    return bom + header + block, src_text[:start] + src_text[end:]


EXT_RE = re.compile(r'\b(\w+)\s*(?:<[^()]*>)?\s*\(\s*(?:\[[^\]]*\]\s*)?this\s+[\w<>\[\],.?]+\s+\w+')


QUAL_RE = re.compile(r'\bAlundra\.Scripts\.(\w+)')
USING_RE = re.compile(r'^[ \t]*using[ \t]+([\w.]+)[ \t]*;[ \t]*$')
PATH_RE = re.compile(r'("Alundra",\s*)"Scripts"(,\s*)"([^"]+\.cs)"')


def own_namespaces(ns):
    """The namespace and every namespace that encloses it."""
    parts = (ns or '').split('.')
    return {'.'.join(parts[:i]) for i in range(1, len(parts) + 1)}


def rewrite_qualifications(src, name_ns):
    """Rewrite `Alundra.Scripts.<T>` (code and cref) for the types that have moved. Returns the new text."""
    edits = []
    for m in QUAL_RE.finditer(src.masked):
        t = m.group(1)
        ns = name_ns.get(t)
        if ns and ns != SCRIPTS_NS:
            edits.append((m.start(), m.start() + len('Alundra.Scripts'), ns))
    for start, val in src.cref_values():
        for m in QUAL_RE.finditer(val):
            t = m.group(1)
            ns = name_ns.get(t)
            if ns and ns != SCRIPTS_NS:
                edits.append((start + m.start(), start + m.start() + len('Alundra.Scripts'), ns))
    text = src.text
    for a, b, ns in sorted(edits, reverse=True):
        text = text[:a] + ns + text[b:]
    return text


def rewrite_paths(text, tb, done, is_test):
    """The source paths a test reads: "Alundra", "Scripts", "<File>.cs" -> the folder of the file once it has moved."""
    if not is_test:
        return text
    mv = tb.file_moves()

    def sub(m):
        old = 'Alundra/Scripts/' + m.group(3)
        new = mv.get(old)
        if not new or Table.domain(new) not in done:
            return m.group(0)
        folders = new.split('/')[1:-1]
        return m.group(1) + ', '.join('"%s"' % f for f in folders) + m.group(2) + '"%s"' % new.split('/')[-1]
    return PATH_RE.sub(sub, text)


def fix_usings(path, text, tb, done, name_ns, ext_ns, is_test):
    """The text with the qualifications, the paths and the using lines recomputed."""
    bom, body = split_bom(text)
    own = file_namespace(text)
    src = Src(body)
    body = rewrite_qualifications(src, name_ns)
    body = rewrite_paths(body, tb, done, is_test)
    src = Src(body)
    names = src.names()
    enclosing = own_namespaces(own)
    needed = set()
    for n in names:
        ns = name_ns.get(n)
        if ns and ns not in enclosing:
            needed.add(ns)
    for m in re.finditer(r'\.\s*(\w+)\s*(?:<[^()]*>)?\s*\(', src.masked):
        ns = ext_ns.get(m.group(1))
        if ns and ns not in enclosing:
            needed.add(ns)
    eol = eol_of(body)
    nm = NS_LINE_RE.search(src.masked)
    if not nm:
        # a file without namespace (InternalsVisibleTo.cs, assembly attributes): nothing to compute
        return bom + body
    head, rest = body[:nm.start()], body[nm.start():]
    lines = head.split('\n')
    # keep the line terminators: split on '\n', every line but the last keeps its '\r' if any
    existing, last_using = {}, -1
    for i, l in enumerate(lines):
        mm = USING_RE.match(l.rstrip('\r'))
        if mm:
            existing[mm.group(1)] = i
        if l.lstrip().startswith('using '):
            last_using = i
    keep_scripts = any(name_ns.get(n) == SCRIPTS_NS for n in names) and own != SCRIPTS_NS
    remove_idx = None
    if SCRIPTS_NS in existing and not keep_scripts:
        remove_idx = existing[SCRIPTS_NS]
    add = sorted(ns for ns in needed if ns not in existing)
    if remove_idx is None and not add:
        return bom + body
    out = list(lines)
    cr = '\r' if eol == '\r\n' else ''
    add_text = [('using %s;' % ns) + cr for ns in add]
    if remove_idx is not None:
        if last_using == remove_idx or not any(i != remove_idx and l.lstrip().startswith('using ') for i, l in enumerate(lines)):
            # the removed line was the last (or the only) using: the additions take its place
            out[remove_idx:remove_idx + 1] = add_text
        else:
            del out[remove_idx]
            last = max(i for i, l in enumerate(out) if l.lstrip().startswith('using '))
            out[last + 1:last + 1] = add_text
    else:
        if last_using >= 0:
            out[last_using + 1:last_using + 1] = add_text
        else:
            # no using at all: the additions, then one blank line, before the namespace line (the head ends with '' after the last '\n')
            idx = len(out) - 1
            out[idx:idx] = add_text + [cr]
    return bom + '\n'.join(out) + rest


def do_fix(tb, domain, dry):
    done = tb.done_set(domain)
    files = source_files()
    written = collections.OrderedDict()   # path -> new text (not yet written)
    fileset = set(files)
    moves = tb.file_moves()

    # precondition: step 1 is done for the domain and the previous ones
    for o, n in moves.items():
        d = Table.domain(n)
        if d in done and (o in fileset or n not in fileset):
            raise R2Error('step 1 is not complete: %s should be at %s' % (o, n))

    def cur(path):
        return written[path] if path in written else read(path)

    # (a) the namespace line of the files of the domain
    ns_files = {}
    for f, m in tb.main.items():
        n = m['r2']['file']
        if Table.domain(n) == domain:
            ns_files[n] = m['r2']['namespace']
    for n, ns in sorted(ns_files.items()):
        if n not in fileset:
            raise R2Error('file of the domain missing: %s' % n)
        t = cur(n)
        t2 = set_namespace(t, ns)
        if t2 != t:
            written[n] = t2

    # (b) the cuts of the types whose target is in the domain
    cuts = [r for r in tb.rows if r['split'] and Table.domain(r['r2']['file']) == domain]
    for r in sorted(cuts, key=lambda x: (x['old_file'], x['old_name'])):
        src_path = tb.cur_file(r, done)
        new_path = r['r2']['file']
        if new_path in fileset or new_path in written:
            raise R2Error('cut target already exists: %s' % new_path)
        if src_path not in fileset:
            raise R2Error('cut source missing: %s' % src_path)
        newtext, rest = cut_type(cur(src_path), r['old_name'], r['r2']['namespace'])
        written[new_path] = newtext
        written[src_path] = rest
        print('cut %s from %s -> %s' % (r['old_name'], src_path, new_path))
    all_files = sorted(set(files) | {p for p in written if p not in fileset})

    # (c) usings, qualifications, paths: for every file
    name_ns = tb.name_ns(done)
    # the files as they will be after (a) and (b) feed the extension-method census
    def reader(p):
        return cur(p)
    ext_ns = {}
    for p in all_files:
        if not p.startswith('Alundra/'):
            continue
        t = cur(p)
        body = split_bom(t)[1]
        if 'this ' not in body:
            continue
        src = Src(body)
        if not EXT_RE.search(src.masked):
            continue
        ext = top_level_extents(src)
        for m in EXT_RE.finditer(src.masked):
            cls = [n for n, (a, b) in ext.items() if a <= m.start() < b]
            if not cls:
                continue
            rows = tb.by_name.get(cls[0])
            if not rows:
                raise R2Error('extension class %s (%s) is not in the table' % (cls[0], p))
            ext_ns[m.group(1)] = tb.cur_ns(rows[0], done)
    changed = 0
    for p in all_files:
        t = cur(p)
        is_test = p.startswith('Alundra.Tests/') or p.startswith('tools/')
        t2 = fix_usings(p, t, tb, done, name_ns, ext_ns, is_test)
        if t2 != t:
            written[p] = t2
    for p, t in written.items():
        isnew = p not in fileset
        if not dry:
            if not isnew and read(p) == t:
                continue
            write(p, t)
            if isnew:
                git('add', '--', p)
        changed += 1
    print('%s --fix: %d file(s) written' % (domain, changed))
    return 0


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument('domain', nargs='?')
    g = ap.add_mutually_exclusive_group()
    g.add_argument('--move', action='store_true')
    g.add_argument('--fix', action='store_true')
    g.add_argument('--domains', action='store_true')
    ap.add_argument('--dry-run', action='store_true')
    ap.add_argument('--allow-dirty', action='store_true')
    a = ap.parse_args()
    sys.stdout.reconfigure(encoding='utf-8')
    if a.domains:
        print(' '.join(ORDER))
        return 0
    if not a.domain or a.domain not in ORDER or not (a.move or a.fix):
        ap.error('a domain of %s and --move or --fix' % ORDER)
    try:
        tb = Table()
        check_tree_clean(a.allow_dirty)
        return do_move(tb, a.domain, a.dry_run) if a.move else do_fix(tb, a.domain, a.dry_run)
    except R2Error as e:
        print('ERROR: %s' % e)
        return 2


if __name__ == '__main__':
    sys.exit(main())
