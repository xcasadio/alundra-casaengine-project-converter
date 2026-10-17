"""Read-only reader of C# type declarations for the Alundra DLL restructuring tool (docs/plan-restructuration-dll.md, R1).

Sources read:
  - the DLL (`Alundra/`): files listed by git (`--cached --others --exclude-standard`, no bin/obj/artifacts), read from the working tree
    (or under an explicit root), or from a pinned git revision;
  - the engine, MGUI and NvgSharp sources at the HEAD of their submodule;
  - the public types of the NuGet packages and of the BCL reference pack, read from their XML documentation.
Nothing is written to the repository. Declarations are found by a regular-expression reader (comments and literals stripped), not by a
compiler: it is exact for the declaration shapes of this code base and is checked by the build in the slices that move files.
"""
import collections
import glob
import os
import re
import subprocess
import sys
import xml.etree.ElementTree as ET
from pathlib import Path

REPO = Path(__file__).resolve().parents[2]
ENGINE = REPO / 'CasaEngineMonogame'
MGUI = ENGINE / 'MGUI'
NVG = ENGINE / 'NvgSharp'
NUGET = Path(os.path.expanduser('~/.nuget/packages'))

# label -> glob (relative to the NuGet cache) of the XML documentation files. A package listed here whose XML is not found is an ERROR.
PACKAGES = {
    'MonoGame.Framework.DesktopGL 3.8.5.1': 'monogame.framework.desktopgl/3.8.5.1/lib/*/*.xml',
    'MonoGame.Extended 6.1.1': 'monogame.extended/6.1.1/lib/*/*.xml',
    'BepuPhysics 2.5.0-beta.29': 'bepuphysics/2.5.0-beta.29/lib/*/*.xml',
    'BepuUtilities 2.5.0-beta.29': 'bepuutilities/2.5.0-beta.29/lib/*/*.xml',
    'FontStashSharp.MonoGame 1.5.7': 'fontstashsharp.monogame/1.5.7/lib/*/*.xml',
    'FontStashSharp.Base': 'fontstashsharp.base/*/lib/*/*.xml',
    'Newtonsoft.Json 13.0.4': 'newtonsoft.json/13.0.4/lib/net6.0/*.xml',
    'NVorbis 0.10.4': 'nvorbis/0.10.4/lib/*/*.xml',
    'Prism.Core 8.1.97': 'prism.core/8.1.97/lib/*/*.xml',
    'SharpGLTF.Core 1.0.6': 'sharpgltf.core/1.0.6/lib/*/*.xml',
    'SharpGLTF.Toolkit 1.0.6': 'sharpgltf.toolkit/1.0.6/lib/*/*.xml',
    'YarnSpinner 3.2.1': 'yarnspinner/3.2.1/lib/*/*.xml',
}
# Packages the DLL's dependency set contains but that ship no XML documentation: named here so that the gap is explicit, never silent.
# The build stays the judge for them (an ambiguous name is CS0104).
PACKAGES_WITHOUT_XML = {
    'Portable.Xaml 0.26.0': 'portable.xaml/0.26.0/lib',
}

BCL_GLOB = 'C:/Program Files/dotnet/packs/Microsoft.NETCore.App.Ref/9.0.*/ref/net9.0'
EXCLUDED_DIR_RE = re.compile(r'(^|/)(bin|obj|artifacts)/')
TEST_RE = r'(Tests?/|\.Tests/|/Tests/|Samples?/|Demo)'


class ScanError(Exception):
    pass


def git(repo, *args, check=True):
    r = subprocess.run(['git', '-C', str(repo), *args], capture_output=True)
    if check and r.returncode != 0:
        raise ScanError('git %s failed in %s: %s' % (' '.join(args), repo, r.stderr.decode('utf-8', 'replace').strip()))
    return r.stdout


def cat_many(repo, rev, paths):
    """Read rev:<path> for every path with one `git cat-file --batch`."""
    if not paths:
        return {}
    inp = ''.join('%s:%s\n' % (rev, p) for p in paths).encode('utf-8')
    raw = subprocess.run(['git', '-C', str(repo), 'cat-file', '--batch'], input=inp, capture_output=True, check=True).stdout
    res, i = {}, 0
    for p in paths:
        nl = raw.index(b'\n', i)
        header = raw[i:nl].decode()
        i = nl + 1
        if header.endswith('missing'):
            continue
        size = int(header.split()[2])
        res[p] = raw[i:i + size].decode('utf-8-sig', errors='replace')
        i += size + 1
    return res


# ---------------------------------------------------------------------------------------------------- declaration reader
STRIP_RE = re.compile(r'//[^\n]*|/\*.*?\*/|@"(?:[^"]|"")*"|\$?"(?:\\.|[^"\\\n])*"|\'(?:\\.|[^\'\\\n])\'', re.S)


def strip(src):
    # newlines are kept so that line numbers survive
    return STRIP_RE.sub(lambda m: '\n' * m.group(0).count('\n') + (' ' if '\n' not in m.group(0) else ''), src)


MODS = r'(?:(?:public|internal|private|protected|static|sealed|abstract|partial|readonly|ref|file|unsafe|new|override|virtual)\s+)*'
DECL_RE = re.compile(r'^[ \t]*(?:\[[^\]]*\]\s*)*' + MODS + r'(class|struct|interface|enum|record\s+struct|record\s+class|record)\s+([A-Za-z_]\w*)', re.M)
DELEG_RE = re.compile(r'^[ \t]*' + MODS + r'delegate\s+[\w<>\[\],.?\s]+?\s+([A-Za-z_]\w*)\s*[<(]', re.M)
NS_RE = re.compile(r'^\s*namespace\s+([\w.]+)\s*(;|\{|$)', re.M)


def parse(path, src):
    """Declarations of one file: list of dicts (path, line, kind, name, ns, nested, access, partial)."""
    s = strip(src)
    depth_at, d = [], 0
    for ch in s:
        depth_at.append(d)
        if ch == '{':
            d += 1
        elif ch == '}':
            d -= 1
    nss = [(m.start(), m.group(1), m.group(2)) for m in NS_RE.finditer(s)]

    def ns_for(off):
        cur, base = '', 0
        for st, name, kind in nss:
            if st <= off:
                cur = name
                base = depth_at[st] + (1 if kind == '{' else 0)
        return cur, base

    out = []
    for m in DECL_RE.finditer(s):
        off = m.start(2)
        ns, base = ns_for(off)
        head = s[m.start():off]
        out.append(dict(path=path, line=s.count('\n', 0, off) + 1, kind=re.sub(r'\s+', ' ', m.group(1)), name=m.group(2), ns=ns,
                        nested=depth_at[off] > base, access=access_of(head), partial=bool(re.search(r'\bpartial\b', head))))
    for m in DELEG_RE.finditer(s):
        off = m.start(1)
        ns, base = ns_for(off)
        head = s[m.start():off]
        out.append(dict(path=path, line=s.count('\n', 0, off) + 1, kind='delegate', name=m.group(1), ns=ns,
                        nested=depth_at[off] > base, access=access_of(head), partial=False))
    out.sort(key=lambda x: (x['line'], x['name']))
    return out


def access_of(head):
    return 'public' if re.search(r'\bpublic\b', head) else ('private' if re.search(r'\bprivate\b', head) else (
        'protected' if re.search(r'\bprotected\b', head) else 'internal'))


# ---------------------------------------------------------------------------------------------------- the DLL
def dll_paths_from_tree(root=None):
    """Repository-relative paths of the DLL's .cs files. root=None: git (cached + untracked, not ignored) in the repository, read in the
    working tree; root given: every .cs under <root>/Alundra (no git)."""
    if root is None:
        out = git(REPO, 'ls-files', '--cached', '--others', '--exclude-standard', '--', 'Alundra').decode('utf-8').splitlines()
        base = REPO
    else:
        base = Path(root)
        out = []
        top = base / 'Alundra'
        if top.is_dir():
            for dp, dn, fn in os.walk(top):
                for f in fn:
                    out.append(os.path.relpath(os.path.join(dp, f), base).replace('\\', '/'))
    paths = sorted({p for p in out if p.endswith('.cs') and not EXCLUDED_DIR_RE.search(p) and (base / p).is_file()})
    return base, paths


def dll_decls(root=None, rev=None):
    """(declarations, paths) of the DLL: from the working tree / root, or from the git revision `rev` (pinned base)."""
    if rev is not None:
        out = git(REPO, 'ls-tree', '-r', '--name-only', rev, '--', 'Alundra').decode('utf-8').splitlines()
        paths = sorted(p for p in out if p.endswith('.cs') and not EXCLUDED_DIR_RE.search(p))
        srcs = cat_many(REPO, rev, paths)
    else:
        base, paths = dll_paths_from_tree(root)
        srcs = {p: (base / p).read_bytes().decode('utf-8-sig', errors='replace') for p in paths}
    decls = []
    for p in paths:
        decls += parse(p, srcs[p])
    return decls, paths


def test_nested_names():
    """Names of the types declared inside another type in Alundra.Tests (working tree): they only mask a namespace segment inside their
    own class body, so the tool reports them as warnings."""
    out = git(REPO, 'ls-files', '--cached', '--others', '--exclude-standard', '--', 'Alundra.Tests').decode('utf-8').splitlines()
    res = []
    for p in sorted(out):
        if p.endswith('.cs') and not EXCLUDED_DIR_RE.search(p) and (REPO / p).is_file():
            for d in parse(p, (REPO / p).read_bytes().decode('utf-8-sig', errors='replace')):
                if d['nested']:
                    res.append(d)
    return res


# ---------------------------------------------------------------------------------------------------- everything else
def _scan_repo(repo, rev, prefixes, exclude_re=None, removes=()):
    out = git(repo, 'ls-tree', '-r', '--name-only', rev, '--', *prefixes).decode('utf-8').splitlines()
    files = [p for p in out if p.endswith('.cs') and not EXCLUDED_DIR_RE.search(p)]
    if exclude_re:
        files = [p for p in files if not re.search(exclude_re, p)]
    files = [p for p in files if p not in removes]
    srcs = cat_many(repo, rev, files)
    decls = []
    for p, src in srcs.items():
        decls += parse(p, src)
    return decls


_external_cache = None


def external_types():
    """{lowercase simple name: [ {label, ns, name, nested, access} ]} for the engine, MGUI, NvgSharp (submodule HEADs), the packages
    and the BCL. Raises ScanError when a source is missing (a listed package without XML, an absent submodule or reference pack)."""
    global _external_cache
    if _external_cache is not None:
        return _external_cache
    for sub in (ENGINE, MGUI, NVG):
        if not (sub / '.git').exists():
            raise ScanError('submodule not checked out: %s' % sub)
    found = collections.defaultdict(list)

    def add(label, decls):
        for d in decls:
            found[d['name'].lower()].append(dict(label=label, ns=d['ns'], name=d['name'], nested=d['nested'], access=d['access']))

    for proj in ('CasaEngine/', 'CasaEngine.Launcher/', 'CasaEngine.Editor/', 'CasaEngine.EditorServices/', 'CasaEngine.Shaders/', 'GizmoTool/'):
        d = _scan_repo(ENGINE, 'HEAD', [proj], TEST_RE)
        if not d:
            raise ScanError('no declaration read in the engine project %s' % proj)
        add('engine:' + proj.rstrip('/'), d)
    csproj = git(MGUI, 'show', 'HEAD:MGUI.Shared/MGUI.Shared.csproj').decode('utf-8-sig')
    removes = {'MGUI.Shared/' + m.group(1).replace('\\', '/') for m in re.finditer(r'Compile Remove="([^"]+)"', csproj)}
    for proj in ('MGUI.Core/', 'MGUI.Shared/', 'MGUI.FontStashSharp/', 'MGUI.MonoGame.Integration/', 'MGUI.Rendering.Abstractions/'):
        d = _scan_repo(MGUI, 'HEAD', [proj], TEST_RE, removes)
        if not d:
            raise ScanError('no declaration read in the MGUI project %s' % proj)
        add('mgui:' + proj.rstrip('/'), d)
    for proj in ('src/NvgSharp/', 'src/XNA/', 'src/NvgSharp.Text/'):
        d = _scan_repo(NVG, 'HEAD', [proj], TEST_RE)
        if not d:
            raise ScanError('no declaration read in NvgSharp %s' % proj)
        add('nvgsharp:' + proj.rstrip('/'), d)

    def from_xml(label, files, kind):
        seen = set()
        for f in files:
            try:
                tree = ET.parse(f)
            except ET.ParseError as e:
                raise ScanError('unreadable XML documentation %s: %s' % (f, e))
            for mem in tree.iter('member'):
                n = mem.get('name', '')
                if n.startswith('T:'):
                    full = re.sub(r'`\d+', '', n[2:])
                    if full not in seen:
                        seen.add(full)
                        parts = full.split('.')
                        found[parts[-1].lower()].append(dict(label=kind + ':' + label, ns='.'.join(parts[:-1]), name=parts[-1], nested=False, access='public'))

    for label, pat in PACKAGES.items():
        files = sorted(glob.glob(str(NUGET / pat)))
        if not files:
            raise ScanError('package %s: no XML documentation found (%s). Add it to PACKAGES_WITHOUT_XML only if the package ships none.' % (label, NUGET / pat))
        from_xml(label, files, 'pkg')
    for label, rel in PACKAGES_WITHOUT_XML.items():
        if not (NUGET / rel).is_dir():
            raise ScanError('package %s: not found in the NuGet cache (%s)' % (label, NUGET / rel))
        if glob.glob(str(NUGET / rel / '*' / '*.xml')):
            raise ScanError('package %s now ships XML documentation: move it to PACKAGES' % label)
    bcl = sorted(glob.glob(BCL_GLOB))
    if not bcl:
        raise ScanError('BCL reference pack not found (%s)' % BCL_GLOB)
    xmls = sorted(glob.glob(bcl[-1] + '/*.xml'))
    if not xmls:
        raise ScanError('no XML documentation in the BCL reference pack %s' % bcl[-1])
    from_xml('net9.0', xmls, 'bcl')
    _external_cache = found
    return found


if __name__ == '__main__':
    sys.stdout.reconfigure(encoding='utf-8')
    decls, paths = dll_decls()
    ext = external_types()
    print('dll files', len(paths), 'declarations', len(decls), 'top-level', sum(1 for d in decls if not d['nested']), 'external names', len(ext))
