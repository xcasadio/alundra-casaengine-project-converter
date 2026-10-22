"""The rules of ADR-0075 and ADR-0080 checked on the living tree, WITHOUT a table (docs/plan-restructuration-dll.md, slice R7).

Used by `check_names.py --rules [--root <folder>]`. Read-only. Nothing here changes `scan_types.py` or the older functions of `check_names.py`
(frozen proofs import them): this module reads the tree itself, masks what the older reader does not model, and calls them.

Rules (numbers of ADR-0075, section 2 of the plan):
  (1) a folder is a namespace: `Alundra` / `Alundra.Tests` + the folder path, one `namespace` per file, every folder segment an identifier
      ([namespace], [dossier]);
  (2) no `Alundra` / `IAlundra` prefix on a type name (nested ones included), a file name or a folder name ([nom]);
  (3) one main type per file: the file name (without a `.Part` suffix) is a top-level type, every public top-level type bears it, except the
      exception written by ADR-0075 ([principal]);
  (4) a simple name exists once: against the engine, MGUI, NvgSharp, the packages (the 13 of the tests included) and the BCL ([collision],
      [segment]), inside the DLL and the tests ([doublon]), no folder named like a type that ADR-0075 rules out ([dossier]);
  (5) the mirror of ADR-0080: every test folder has its counterpart in the DLL, except Arcs, Support and ExportGuards ([miroir]).
Reader integrity: [lecture] (braces that do not balance after masking, a type declaration that does not start its line).
Warning: a nested type named like a namespace segment ([avertissement]).
"""
import collections
import os
import re
import sys
from pathlib import Path

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import scan_types  # noqa: E402
import check_names  # noqa: E402

HERE = os.path.dirname(os.path.abspath(__file__))
EXTRA_TYPES_FILE = os.path.join(HERE, 'rules-extra-types.tsv')
ROOTS = (('dll', 'Alundra', 'Alundra'), ('tests', 'Alundra.Tests', 'Alundra.Tests'))
FORBIDDEN_FOLDERS = ('World', 'Player', 'Camera', 'Dialogue', 'TextBox', 'Text', 'Debug', 'Math', 'Entity', 'SaveScreen')
MIRROR_EXEMPT = ('Arcs', 'Support', 'ExportGuards')
# ADR-0075: the one public secondary type that has no file of its own
PUBLIC_SECONDARY = {'Alundra/Events/IEventProgramRunner.cs': {'NoOpEventProgramRunner'}}

# comments and literals as scan_types.STRIP_RE, plus the raw string literals ("""...""" , $"""...""") that it does not model
MASK_RE = re.compile(r'(?P<raw>\$*(?P<q>"{3,}).*?(?P=q))|' + scan_types.STRIP_RE.pattern, re.S)
LATE_RE = re.compile(r'[;{}][ \t]*(?:\[[^\]\n]*\][ \t]*)*' + scan_types.MODS +
                     r'(?:class|struct|interface|enum|record\s+struct|record\s+class|record)\s+[A-Za-z_]\w*')
IDENT_RE = re.compile(r'^[A-Za-z_]\w*$')


def mask(src):
    """Comments, strings, char literals and raw string literals replaced by blanks; newlines kept (line numbers survive)."""
    return MASK_RE.sub(lambda m: '\n' * m.group(0).count('\n') + (' ' if '\n' not in m.group(0) else ''), src)


def _list_cs(base, prefix, use_git):
    if use_git:
        out = scan_types.git(base, 'ls-files', '--cached', '--others', '--exclude-standard', '--', prefix).decode('utf-8').splitlines()
    else:
        out = []
        top = Path(base) / prefix
        if top.is_dir():
            for dp, _dn, fn in os.walk(top):
                for f in fn:
                    out.append(os.path.relpath(os.path.join(dp, f), base).replace('\\', '/'))
    return sorted({p for p in out if p.endswith('.cs') and not scan_types.EXCLUDED_DIR_RE.search(p) and (Path(base) / p).is_file()})


def _extra_externals():
    """The public top-level types that the tests see and that `scan_types.external_types()` does not read: the 13 packages of
    `rules-extra-types.tsv` (see gen_extra_types.py) and the project CasaEngine.Compiler of the engine (sources at its HEAD)."""
    if not os.path.isfile(EXTRA_TYPES_FILE):
        raise scan_types.ScanError('missing %s (generate it with gen_extra_types.py)' % EXTRA_TYPES_FILE)
    found = collections.defaultdict(list)
    for line in open(EXTRA_TYPES_FILE, encoding='utf-8'):
        if not line.strip():
            continue
        label, ns, name = line.rstrip('\n').split('\t')
        found[name.lower()].append(dict(label='pkg:' + label, ns=ns, name=name, nested=False, access='public'))
    decls = scan_types._scan_repo(scan_types.ENGINE, 'HEAD', ['CasaEngine.Compiler/'], scan_types.TEST_RE)
    if not decls:
        raise scan_types.ScanError('no declaration read in the engine project CasaEngine.Compiler')
    for d in decls:
        found[d['name'].lower()].append(dict(label='engine:CasaEngine.Compiler', ns=d['ns'], name=d['name'], nested=d['nested'], access=d['access']))
    return found


def externals():
    ext = {k: list(v) for k, v in scan_types.external_types().items()}
    for k, v in _extra_externals().items():
        ext.setdefault(k, []).extend(v)
    return ext


def read_tree(root=None):
    """(decls, paths, srcs) per key ('dll', 'tests'), the sources already masked. root=None: the working tree of the repository, read through
    git (cached + untracked, not ignored); root given: <root>/Alundra and <root>/Alundra.Tests (through git when <root> is a repository)."""
    base = Path(scan_types.REPO) if root is None else Path(root)
    use_git = root is None or (base / '.git').exists()
    decls, paths, srcs = {}, {}, {}
    for key, rdir, _rns in ROOTS:
        paths[key] = _list_cs(base, rdir, use_git)
        decls[key] = []
        for p in paths[key]:
            srcs[p] = mask((base / p).read_bytes().decode('utf-8-sig', errors='replace'))
            decls[key] += scan_types.parse(p, srcs[p])
    return decls, paths, srcs


def _expected_ns(path, rdir, rns):
    folder = path[len(rdir) + 1:].rpartition('/')[0]
    return rns + ('.' + folder.replace('/', '.') if folder else '')


def _balanced(masked):
    d = 0
    for ch in masked:
        if ch == '{':
            d += 1
        elif ch == '}':
            d -= 1
            if d < 0:
                return False
    return d == 0


def check_rules(root=None):
    """Returns (findings, warnings, head). Raises scan_types.ScanError when an external source is missing."""
    ext = externals()
    decls, paths, srcs = read_tree(root)
    findings, warnings = [], []
    allpaths = paths['dll'] + paths['tests']

    # reader integrity
    for p in allpaths:
        if not _balanced(srcs[p]):
            findings.append('[lecture] %s: braces do not balance after masking comments and literals (the declaration reader cannot be trusted here)' % p)
        for m in LATE_RE.finditer(srcs[p]):
            findings.append('[lecture] %s:%d: a type declaration that does not start its line (the declaration reader would not see it)' % (
                p, srcs[p].count('\n', 0, m.start()) + 1))

    # (1) a folder is a namespace
    for key, rdir, rns in ROOTS:
        for p in paths[key]:
            exp = _expected_ns(p, rdir, rns)
            nss = [m.group(1) for m in scan_types.NS_RE.finditer(srcs[p])]
            if len(nss) > 1:
                findings.append('[namespace] %s has %d namespace statements (%s), expected one' % (p, len(nss), ', '.join(nss)))
            top = [x for x in decls[key] if x['path'] == p and not x['nested']]
            for g in sorted({x['ns'] for x in top}):
                if g != exp:
                    findings.append('[namespace] %s: %s in namespace "%s", expected "%s" (its folder)' % (
                        p, ','.join(sorted(x['name'] for x in top if x['ns'] == g))[:80], g or '(global)', exp))

    # (2) no Alundra prefix
    for key, rdir, _rns in ROOTS:
        for x in decls[key]:
            if x['name'].startswith('Alundra') or x['name'].startswith('IAlundra'):
                findings.append('[nom] %s:%d declares %s%s with the prefix' % (x['path'], x['line'], x['name'], ' (nested)' if x['nested'] else ''))
        for p in paths[key]:
            segs = p[len(rdir) + 1:].split('/')
            if segs[-1].startswith('Alundra') or segs[-1].startswith('IAlundra'):
                findings.append('[nom] file name %s keeps the prefix' % p)
            for sg in segs[:-1]:
                if sg.startswith('Alundra') or sg.startswith('IAlundra'):
                    findings.append('[nom] folder %s keeps the prefix (%s)' % (sg, p))

    # (3) one main type per file
    for key, rdir, _rns in ROOTS:
        for p in paths[key]:
            fname = p.rsplit('/', 1)[-1][:-3]
            stem = fname.split('.')[0]
            top = [x for x in decls[key] if x['path'] == p and not x['nested']]
            if not top:
                continue
            bears = [x for x in top if x['name'] == stem]
            if not bears:
                findings.append('[principal] %s: no top-level type bears the file name (%s)' % (p, ','.join(sorted({x['name'] for x in top}))))
            elif fname != stem and not any(x['partial'] for x in bears):
                findings.append('[principal] %s: a `.Part` file whose type %s is not partial' % (p, stem))
            allowed = PUBLIC_SECONDARY.get(p, set())
            for x in top:
                if x['access'] == 'public' and x['name'] != stem and x['name'] not in allowed:
                    findings.append('[principal] %s:%d: public secondary type %s (it takes its own file)' % (p, x['line'], x['name']))

    # (4) a simple name exists once
    top_all = [(x['path'], x['ns'], x['name']) for k in ('dll', 'tests') for x in decls[k] if not x['nested']]
    findings += check_names.collision_findings(top_all, ext)
    byname = collections.defaultdict(list)
    for k in ('dll', 'tests'):
        for x in decls[k]:
            if not x['nested']:
                byname[x['name']].append(x)
    for name, lst in sorted(byname.items()):
        if len(lst) < 2:
            continue
        groups = collections.defaultdict(list)
        for x in lst:
            groups[x['ns']].append(x)
        bad = len(groups) > 1 or any(len(g) > 1 and any(not x['partial'] for x in g) for g in groups.values())
        if bad:
            findings.append('[doublon] name %s is declared in %d places: %s' % (
                name, len(lst), '; '.join('%s:%d (%s)' % (x['path'], x['line'], x['ns']) for x in lst)))
    segs = {s for _, ns, _ in top_all for s in (ns or '').split('.') if s}
    for k in ('dll', 'tests'):
        for x in decls[k]:
            if x['nested'] and x['name'] in segs:
                warnings.append('[avertissement] %s:%d: the nested type %s has the name of a namespace segment (it only masks it inside its own class)' % (
                    x['path'], x['line'], x['name']))

    # folders: identifiers, forbidden names, the mirror (5)
    folders = {'dll': set(), 'tests': set()}
    for key, rdir, _rns in ROOTS:
        for p in paths[key]:
            parts = p[len(rdir) + 1:].split('/')[:-1]
            for i in range(len(parts)):
                folders[key].add('/'.join(parts[:i + 1]))
    for key, _rdir, _rns in ROOTS:
        for f in sorted(folders[key]):
            for seg in f.split('/'):
                if not IDENT_RE.match(seg):
                    findings.append('[dossier] %s folder %s: segment %r is not an identifier' % (key, f, seg))
                if seg in FORBIDDEN_FOLDERS:
                    findings.append('[dossier] %s folder %s: segment %s is a name that ADR-0075 rules out' % (key, f, seg))
    for f in sorted(folders['tests']):
        if f.split('/')[0] in MIRROR_EXEMPT:
            continue
        if f not in folders['dll']:
            findings.append('[miroir] test folder %s has no counterpart folder in the DLL (exempt: %s)' % (f, ', '.join(MIRROR_EXEMPT)))

    ntop = len(top_all)
    nn = sum(1 for k in ('dll', 'tests') for x in decls[k] if x['nested'])
    head = 'rules: %d DLL files, %d test files, %d top-level types, %d nested types' % (len(paths['dll']), len(paths['tests']), ntop, nn)
    return findings, warnings, head
