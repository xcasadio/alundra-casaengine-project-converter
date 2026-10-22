"""R5 discovery (read-only): shared loaders. Never writes in the repository.

Everything is read from the working tree / git of D:/development/repo/alundra-casaengine-project-converter.
Reuses (read-only) tools/dll-structure/{scan_types,apply_r2,check_names}.py: the declaration reader, the C# lexer and the collision checks.
"""
import collections
import json
import os
import re
import subprocess
import sys

sys.dont_write_bytecode = True

REPO = 'D:/development/repo/alundra-casaengine-project-converter'
TOOLS = REPO + '/tools/dll-structure'
HERE = os.path.dirname(os.path.abspath(__file__))
# the generated intermediates (evidence.json, couplings.json, checks.json...) go to WORK, not into the repository
WORK = os.environ.get('R5_WORK', HERE)
sys.path.insert(0, TOOLS)

import scan_types  # noqa: E402
import apply_r2 as A  # noqa: E402
import check_names  # noqa: E402

PREFIX = 'Alundra'


def git_lines(*args):
    r = subprocess.run(['git', '-C', REPO, *args], capture_output=True)
    if r.returncode != 0:
        raise RuntimeError('git %s: %s' % (' '.join(args), r.stderr.decode('utf-8', 'replace')))
    return r.stdout.decode('utf-8').splitlines()


def read_text(rel):
    return open(REPO + '/' + rel, 'rb').read().decode('utf-8-sig', errors='replace')


def test_files():
    """All tracked files of Alundra.Tests."""
    return sorted(git_lines('ls-files', '--', 'Alundra.Tests'))


def folder_of(final_file):
    """Alundra/Rendering/Backdrops/BackdropStage.cs -> Rendering/Backdrops ; Alundra/Plugin.cs -> '' ."""
    d = final_file.rsplit('/', 1)[0]
    d = d[len('Alundra'):].lstrip('/')
    return d


def load_table():
    return json.load(open(TOOLS + '/table.json', encoding='utf-8'))


def dll_world(table):
    """The DLL as it is NOW (working tree, after R3) and where each type ends up ('final').
    Returns dict: cur_full -> dict(cur_ns, cur_name, final_ns, final_name, final_folder, data_cited); name_idx: simple name (cur or final) -> [cur_full]."""
    decls, paths = scan_types.dll_decls()
    top = [d for d in decls if not d['nested']]
    by_r3 = collections.defaultdict(list)
    for r in table['types']:
        by_r3[(r['r3']['namespace'], r['r3']['name'])].append(r)
        if (r['final']['namespace'], r['final']['name']) != (r['r3']['namespace'], r['r3']['name']):
            by_r3[(r['final']['namespace'], r['final']['name'])].append(r)  # after R4: the tree is at the final stage
    cur = {}
    unmatched = []
    for d in top:
        rows = by_r3.get((d['ns'], d['name']))
        if not rows:
            unmatched.append(d)
            continue
        r = rows[0]
        full = '%s.%s' % (d['ns'], d['name'])
        cur[full] = dict(cur_ns=d['ns'], cur_name=d['name'], final_ns=r['final']['namespace'], final_name=r['final']['name'],
                         final_folder=folder_of(r['final']['file']), data_cited=r['data_cited'], kind=d['kind'], access=d['access'],
                         path=d['path'])
    for rt in table['root_files']:
        for t in rt['types']:
            full = '%s.%s' % (rt['namespace'], t)
            cur[full] = dict(cur_ns=rt['namespace'], cur_name=t, final_ns=rt['namespace'], final_name=t, final_folder='', data_cited=False,
                             kind='class', access='public', path=rt['final'])
    name_idx = collections.defaultdict(set)
    for full, v in cur.items():
        name_idx[v['cur_name']].add(full)
        name_idx[v['final_name']].add(full)
    return cur, name_idx, unmatched, paths


NS_SEGMENTS_DLL = None


def dll_segments(cur):
    s = set()
    for v in cur.values():
        for seg in v['final_ns'].split('.'):
            if seg:
                s.add(seg)
        for seg in v['cur_ns'].split('.'):
            if seg:
                s.add(seg)
    return s


# ------------------------------------------------------------------------------------------------ test file parsing
FACT_RE = re.compile(r'\[\s*(?:Fact|Theory|[A-Za-z]*Fact|[A-Za-z]*Theory)\b')
USING_RE = re.compile(r'^[ \t]*(global\s+)?using\s+(static\s+)?(?:(\w+)\s*=\s*)?([\w.]+)\s*;', re.M)
IDENT_RE = re.compile(r'@?[A-Za-z_]\w*')


class TestFile:
    def __init__(self, rel):
        self.rel = rel
        self.text = read_text(rel)
        self.src = A.Src(self.text)
        self.masked = self.src.masked
        self.decls = scan_types.parse(rel, self.text)
        self.top = [d for d in self.decls if not d['nested']]
        self.nested = [d for d in self.decls if d['nested']]
        nss = sorted({d['ns'] for d in self.decls})
        self.ns = nss[0] if nss else ''
        self.nss = nss
        self.n_tests = len(FACT_RE.findall(self.masked))
        self.lines = self.text.count('\n') + 1
        self.usings = [m.group(4) for m in USING_RE.finditer(self.masked)]
        self.base = rel.rsplit('/', 1)[-1]
        self._idents = None

    def idents(self):
        """[(offset, name, qualifier)] of every identifier in code (comments and strings masked). qualifier = the identifier left of a '.'
        immediately before (None when not preceded by '.'); '?.' / ').' / ']' chains give qualifier ''(expression)."""
        if self._idents is None:
            out = []
            m_ = self.masked
            for m in IDENT_RE.finditer(m_):
                s = m.start()
                j = s - 1
                while j >= 0 and m_[j] in ' \t\r\n':
                    j -= 1
                q = None
                if j >= 0 and m_[j] == '.' and not (j >= 1 and m_[j - 1] == '.'):
                    k = j - 1
                    while k >= 0 and m_[k] in ' \t\r\n':
                        k -= 1
                    e = k + 1
                    while k >= 0 and (m_[k].isalnum() or m_[k] == '_'):
                        k -= 1
                    q = m_[k + 1:e] if e > k + 1 else ''
                out.append((s, m.group(0).lstrip('@'), q))
            self._idents = out
        return self._idents

    def is_directive_line(self, off):
        ls = self.masked.rfind('\n', 0, off) + 1
        head = self.masked[ls:off].strip()
        return head.startswith('using') or head.startswith('namespace') or head.startswith('global using')


def new_name(name):
    """Alundra prefix removed: AlundraX -> X, IAlundraX -> IX. Unchanged when no prefix."""
    if name.startswith('IAlundra') and len(name) > 8 and name[8].isupper():
        return 'I' + name[8:]
    if name.startswith(PREFIX) and len(name) > len(PREFIX) and name[len(PREFIX)].isupper():
        return name[len(PREFIX):]
    return name
