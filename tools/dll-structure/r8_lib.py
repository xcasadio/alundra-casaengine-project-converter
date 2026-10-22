"""R8 (docs/plan-restructuration-dll.md, section 13): what `gen_r8.py`, `apply_r8.py`, `prove_r8.py` and `selftest_r8.py` share.

R8 rewrites the OLD names that still stand in the COMMENTS of the DLL (`Alundra/`) and of the tests (`Alundra.Tests/`) to the names of today,
line for line: an old type name (R3, R4, R5), an old file name, an old qualified name, and the few paths / tokens that the rules cannot decide
(the "hand cases", P-R8-3, each with its written target).  Nothing but a comment changes.

A CANDIDATE is a place of a comment where one of those old names stands.  It is located by file, line and RANK (its order among the candidates
of the line), which is stable because R8 changes no line.  The survey `r8-comments.tsv` has one row per candidate:
    file, line, rank, kind, old, new, class, reason
class `auto` = rewritten by the table; `hand` = rewritten by a written case of P-R8-3; `kept` = left as it is, with the reason (new = old).

The comments are found by the lexer of R2 (`apply_r2.py`, kind `c`).  The two files that are not C# (`Alundra.Tests/xunit.runner.json`, the
`_comment` value; `Alundra.Tests/Alundra.Tests.csproj`, the XML comments) have their own reader (`other_regions`).
Python 3, standard library only; run with `python -B`.
"""
import bisect
import collections
import json
import os
import re
import subprocess
import sys

sys.dont_write_bytecode = True
HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.dirname(os.path.dirname(HERE)).replace('\\', '/')
sys.path.insert(0, HERE)
import apply_r2 as A  # noqa: E402

SURVEY = HERE + '/r8-comments.tsv'
ROOTS = ('Alundra/', 'Alundra.Tests/')
OTHER_FILES = ('Alundra.Tests/xunit.runner.json', 'Alundra.Tests/Alundra.Tests.csproj')
SKIP_DIRS = ('bin', 'obj', 'artifacts')
COLS = ['file', 'line', 'rank', 'kind', 'old', 'new', 'class', 'reason']


# ------------------------------------------------------------------------------------------------------------------ git and files
def git(*args, repo=REPO):
    r = subprocess.run(['git', '-C', repo, *args], capture_output=True)
    if r.returncode != 0:
        raise RuntimeError(r.stderr.decode('utf-8', 'replace'))
    return r.stdout


def is_perimeter_cs(p):
    return p.endswith('.cs') and p.startswith(ROOTS)


def scope_ok(path, scope):
    if scope == 'dll':
        return path.startswith('Alundra/')
    if scope == 'tests':
        return path.startswith('Alundra.Tests/')
    return path.startswith(ROOTS)


def list_cs_git(rev=None):
    if rev is None:
        out = git('ls-files', '-z', '--', 'Alundra', 'Alundra.Tests').decode('utf-8').split('\0')
    else:
        out = git('ls-tree', '-r', '-z', '--name-only', rev, '--', 'Alundra', 'Alundra.Tests').decode('utf-8').split('\0')
    return sorted(p for p in out if is_perimeter_cs(p))


def list_cs_dir(root):
    out = []
    for top in ('Alundra', 'Alundra.Tests'):
        for dp, dn, fn in os.walk(os.path.join(root, top)):
            dn[:] = [d for d in dn if d not in SKIP_DIRS]
            for f in fn:
                if f.endswith('.cs'):
                    out.append(os.path.relpath(os.path.join(dp, f), root).replace('\\', '/'))
    return sorted(out)


def read_bytes(root, path):
    with open(os.path.join(root, path), 'rb') as f:
        return f.read()


def write_bytes(root, path, data):
    with open(os.path.join(root, path), 'wb') as f:
        f.write(data)


def blobs_at(rev, paths):
    """{path: bytes} of the blobs of a revision (one `git cat-file --batch`)."""
    p = subprocess.Popen(['git', '-C', REPO, 'cat-file', '--batch'], stdin=subprocess.PIPE, stdout=subprocess.PIPE)
    out = {}
    for path in paths:
        p.stdin.write(('%s:%s\n' % (rev, path)).encode('utf-8'))
        p.stdin.flush()
        head = p.stdout.readline().decode('utf-8').split()
        if len(head) != 3 or head[1] != 'blob':
            raise RuntimeError('no blob %s:%s (%s)' % (rev, path, ' '.join(head)))
        size = int(head[2])
        data = p.stdout.read(size)
        p.stdout.read(1)
        out[path] = data
    p.stdin.close()
    p.wait()
    return out


# ------------------------------------------------------------------------------------------------------------------ tables
def read_tsv(path):
    with open(path, encoding='utf-8', newline='') as f:
        lines = f.read().replace('\r\n', '\n').split('\n')
    head = lines[0].split('\t')
    rows = []
    for ln in lines[1:]:
        if not ln:
            continue
        cells = ln.split('\t')
        cells += [''] * (len(head) - len(cells))
        rows.append(dict(zip(head, cells)))
    return rows


class Maps:
    """The old -> new maps of the three tables (table.json: R3 and R4; r5-types.tsv, r5-nested-types.tsv: R5)."""

    def __init__(self):
        t = json.load(open(HERE + '/table.json', encoding='utf-8'))
        names = collections.defaultdict(set)
        self.dll_full = {}
        for r in t['types']:
            o, n = r['old_name'], r['final']['name']
            if o != n:
                names[o].add(n)
            self.dll_full['Alundra.Scripts.' + o] = r['final']['namespace'] + '.' + n
        self.test_full = {}
        for r in read_tsv(HERE + '/r5-types.tsv'):
            if r['old_type'] != r['new_type']:
                names[r['old_type']].add(r['new_type'])
            if r['old_full'] != r['new_full']:
                self.test_full[r['old_full']] = r['new_full']
        for r in read_tsv(HERE + '/r5-nested-types.tsv'):
            names[r['old_type']].add(r['new_type'])
        bad = {o: v for o, v in names.items() if len(v) != 1}
        if bad:
            raise SystemExit('old names with several new names: %r' % bad)
        self.new = {o: next(iter(v)) for o, v in names.items()}
        # old file basename -> new basename (unique), for the files of the DLL and of the tests
        old_main = collections.defaultdict(set)
        old_paths = collections.defaultdict(list)  # old path -> [(new path, is main)]
        for r in t['types']:
            old_paths[r['old_file']].append((r['final']['file'], r['main']))
        for r in t['root_files']:
            old_paths[r['old_file']].append((r['final'], True))
        for r in read_tsv(HERE + '/r5-files.tsv'):
            if r['old_path']:
                old_paths[r['old_path']].append((r['new_path'], True))
            elif r['source_file']:
                old_paths[r['source_file']].append((r['new_path'], False))
        base_cnt = collections.Counter(os.path.basename(p) for p in old_paths)
        for p, lst in old_paths.items():
            mains = {os.path.basename(x[0]) for x in lst if x[1]}
            if len(mains) == 1:
                old_main[os.path.basename(p)].add(next(iter(mains)))
        self.file_new = {b: next(iter(v)) for b, v in old_main.items()
                         if len(v) == 1 and b.startswith(('Alundra', 'IAlundra')) and base_cnt[b] == 1}
        alts = sorted(self.new, key=len, reverse=True)
        self.name_alt = '|'.join(map(re.escape, alts))


MAPS = Maps()
NAME_RX = re.compile(r'(?<![A-Za-z0-9_])(?:' + MAPS.name_alt + r')(?![A-Za-z0-9_])')

# The alternatives, in priority order (the first one that matches at a position wins).
RX = re.compile('|'.join([
    r'(?P<cref><see cref="AlundraDialogueSpeakerOracle"/>)',
    r'(?P<nsscripts>(?<![\w])Alundra\.Scripts(?:\.(?:' + MAPS.name_alt + r')(?![A-Za-z0-9_]))?)',
    r'(?P<nstests>(?<![\w])Alundra\.Tests(?:\.[A-Za-z_]\w*)+)',
    r'(?P<tpath>(?<![\w])Alundra\.Tests[/\\]IntroTraceHarnessTests\.cs)',
    r'(?P<spath>(?<![\w])Alundra/Scripts(?![\w])(?:/[\w.\-*]*)?)',
    r'(?P<gpath>(?<![A-Za-z0-9_])UI/TextBoxGpuHarness\.cs)',
    r'(?P<file>(?<![\w])I?Alundra\w*\.(?:cs|xaml|json|tsv|md)(?![\w]))',
    r'(?P<name>(?<![A-Za-z0-9_])(?:' + MAPS.name_alt + r')(?![A-Za-z0-9_]))',
    r'(?P<arc>(?<![A-Za-z0-9_])AlundraArcSupport(?![A-Za-z0-9_]))',
]))

# ------------------------------------------------------------------------------------------------------------------ the hand cases (P-R8-3)
KEPT_FILES = {
    'AlundraDisplay.cs': 'name of a file of the decompilation (AlundraTools / AlundraEngine), never an old name of the DLL (P-R8-3 (a))',
    'AlundraGame.cs': 'name of a file of the analyzer (alundra-datas-analyser), never an old name of the DLL (P-R8-3 (a))',
    'AlundraGame.json': 'name of a data file, never an old name of the DLL (P-R8-3 (a))',
}
HERO = 'Alundra/Hero/HeroController.cs'
SCREEN_FADE = 'Alundra.Tests/Transitions/ScreenFadeCameraWiringTests.cs'
INTRO_FILES = ('Alundra/Events/EventProgramRunner.cs', 'Alundra/Physics/EntitySupport.cs',
               'Alundra.Tests/Audio/SoundOpcodesProductionTests.cs', 'Alundra.Tests/Cells/CellStoreProductionTests.cs')
FONT3 = 'Alundra.Tests/UI/Speech/Font3GlyphTests.cs'
ARC_FILE = 'Alundra.Tests/Events/MissingAnimationWaitCorpusTests.cs'
SPEAKER_FILE = 'Alundra.Tests/Dialogues/DialogueSpeakerMachineTests.cs'


def hand_case(path, grp, tok):
    """(new, class, reason, kind) for a hand-case token, or None (= a stop: the rules do not decide it)."""
    if grp == 'file' and tok in KEPT_FILES:
        return tok, 'kept', KEPT_FILES[tok], 'file'
    if grp == 'spath':
        if tok.startswith('Alundra/Scripts/PlayerManager.cs') and path == HERO:
            return tok, 'kept', ('citation of a note of the frozen plan docs/plan-conversion-totale.md (E2-A): "quoted by the converter\'s '
                                 'own E2-A"; the file never existed under this name (P-R8-3 (a))'), 'path'
        if tok == 'Alundra/Scripts' and path == SCREEN_FADE:
            return 'Alundra/', 'hand', 'the sentence speaks of the whole DLL ("nothing under ... references / overrides"), P-R8-3 (b)', 'path'
        return None
    if grp == 'tpath':
        if path in INTRO_FILES:
            new = tok.replace('IntroTraceHarnessTests.cs', 'Arcs' + ('\\' if '\\' in tok else '/') + 'HeadlessIntroSimulation.cs')
            return new, 'hand', ('the file IntroTraceHarnessTests.cs was cut by R5: the simulation is in Arcs/HeadlessIntroSimulation.cs, '
                                 'root and separator of the original kept, P-R8-3 (c)'), 'path'
        return None
    if grp == 'gpath':
        if path == FONT3:
            return 'UI/Shared/TextBoxGpu.cs', 'hand', ('UI/TextBoxGpuHarness.cs was cut by R5: UI/Shared/TextBoxGpu.cs declares the type '
                                                       'cited (Alundra.Tests.UI.Shared.TextBoxGpu), P-R8-3 (c)'), 'path'
        return None
    if grp == 'arc':
        if path == ARC_FILE:
            return 'ArcPrefabAssets', 'hand', ('AlundraArcSupport was a file of helpers cut by R5; the type that resolves the prefab today is '
                                               'ArcPrefabAssets (Alundra.Tests/Support/ArcPrefabAssets.cs:55), P-R8-3 (d)'), 'token'
        return None
    if grp == 'cref':
        if path == SPEAKER_FILE:
            return ('<c>Alundra.Tests/Support/SpeakerAnnex.cs</c>', 'hand',
                    ('the cref aims at no type (AlundraDialogueSpeakerOracle, since f4af632d); the file that replaced it declares no single '
                     'type of the oracle (SpeakerAnnex, OracleSlide, OracleNameBox, OraclePortrait), P-R8-3 (e)'), 'cref')
        return None
    return None


# ------------------------------------------------------------------------------------------------------------------ candidates
class Cand:
    __slots__ = ('start', 'end', 'line', 'rank', 'kind', 'old', 'new', 'cls', 'reason')

    def __init__(self, start, end, line, kind, old, new, cls, reason):
        self.start, self.end, self.line, self.kind, self.old, self.new, self.cls, self.reason = start, end, line, kind, old, new, cls, reason
        self.rank = 0


def line_starts(body):
    out = [0]
    for m in re.finditer('\n', body):
        out.append(m.end())
    return out


def scan(path, body, regions):
    """The candidates of the regions [(a, b)] of a text, in file order, ranked per line.  cls `stop` = the rules do not decide it."""
    ls = line_starts(body)
    out = []
    for a, b in regions:
        text = body[a:b]
        for m in RX.finditer(text):
            grp, tok = m.lastgroup, m.group(0)
            s, e = a + m.start(), a + m.end()
            line = bisect.bisect_right(ls, s)
            if grp == 'name':
                out.append(Cand(s, e, line, 'name', tok, MAPS.new[tok], 'auto', ''))
            elif grp == 'nsscripts':
                new = MAPS.dll_full.get(tok)
                if new is not None and tok.count('.') == 2:
                    out.append(Cand(s, e, line, 'qualified-dll', tok, new, 'auto', ''))
                else:
                    out.append(Cand(s, e, line, 'qualified-dll', tok, tok, 'stop', 'Alundra.Scripts without a type of the table'))
            elif grp == 'nstests':
                parts = tok.split('.')
                done = False
                for i in range(len(parts), 2, -1):
                    cand = '.'.join(parts[:i])
                    if cand in MAPS.test_full:
                        out.append(Cand(s, s + len(cand), line, 'qualified-test', cand, MAPS.test_full[cand], 'auto', ''))
                        done = True
                        break
                if not done and NAME_RX.search(tok):
                    out.append(Cand(s, e, line, 'qualified-test', tok, tok, 'stop', 'a qualified name with an old name inside'))
            elif grp == 'file':
                if tok in KEPT_FILES:
                    new, cls, reason, kind = hand_case(path, grp, tok)
                    out.append(Cand(s, e, line, kind, tok, new, cls, reason))
                elif tok in MAPS.file_new:
                    out.append(Cand(s, e, line, 'file', tok, MAPS.file_new[tok], 'auto', ''))
                else:
                    out.append(Cand(s, e, line, 'file', tok, tok, 'stop', 'an Alundra file name that is not in the tables'))
            else:  # hand cases
                h = hand_case(path, grp, tok)
                if h is None:
                    out.append(Cand(s, e, line, grp, tok, tok, 'stop', 'a hand-case token outside its written case'))
                else:
                    out.append(Cand(s, e, line, h[3], tok, h[0], h[1], h[2]))
    out.sort(key=lambda c: c.start)
    last, rank = None, 0
    for c in out:
        rank = rank + 1 if c.line == last else 1
        last = c.line
        c.rank = rank
    return out


def cs_candidates(path, body):
    src = A.Src(body)
    return scan(path, body, [(a, b) for a, b, k in src.spans if k == 'c'])


def other_regions(path, body):
    """The comment regions of a file that is not C#: the value of `_comment` of the JSON, the contents of the XML comments of the project."""
    if path.endswith('.json'):
        m = re.search(r'"_comment"\s*:\s*"((?:[^"\\]|\\.)*)"', body)
        if not m:
            raise SystemExit('%s: no _comment value' % path)
        if json.loads(body)['_comment'] != json.loads('"' + m.group(1) + '"'):
            raise SystemExit('%s: the _comment found is not the _comment key' % path)
        return [(m.start(1), m.end(1))]
    if path.endswith('.csproj'):
        return [(m.start(1), m.end(1)) for m in re.finditer(r'<!--(.*?)-->', body, re.S)]
    raise SystemExit('unknown file ' + path)


def other_candidates(path, body):
    regions = other_regions(path, body)
    cands = scan(path, body, regions)
    # nothing outside the regions may carry an old name (otherwise the file is not what P-R8-1 says)
    outside = []
    ls = line_starts(body)
    for m in RX.finditer(body):
        if not any(a <= m.start() < b for a, b in regions):
            outside.append((bisect.bisect_right(ls, m.start()), m.group(0)))
    outside = [o for o in outside if o[1] != 'Alundra.Tests' and not o[1].startswith('Alundra.Tests.')]
    if outside:
        raise SystemExit('%s: an old name outside the comment: %r' % (path, outside))
    return cands


# ------------------------------------------------------------------------------------------------------------------ the survey
def load_survey():
    """{file: {(line, rank): row}} of r8-comments.tsv, rows with int line / rank."""
    by_file = collections.defaultdict(dict)
    for r in read_tsv(SURVEY):
        r['line'], r['rank'] = int(r['line']), int(r['rank'])
        key = (r['line'], r['rank'])
        if key in by_file[r['file']]:
            raise SystemExit('survey: %s %r twice' % (r['file'], key))
        by_file[r['file']][key] = r
    return by_file


def decode(raw):
    """(bom, body) of the bytes, as latin-1 (every byte kept)."""
    return A.split_bom(raw.decode('latin-1'))


def encode(bom, body):
    return (bom + body).encode('latin-1')


def match_rows(path, cands, rows):
    """Errors where the candidates of a file and its survey rows do not say the same thing (same keys, same old text)."""
    errs = []
    keys = {(c.line, c.rank): c for c in cands}
    for k, c in keys.items():
        r = rows.get(k)
        if r is None:
            errs.append('%s:%d rank %d: candidate %r (%s) is not in the survey' % (path, k[0], k[1], c.old, c.cls))
        elif r['old'] != c.old:
            errs.append('%s:%d rank %d: survey says %r, the file has %r' % (path, k[0], k[1], r['old'], c.old))
    for k, r in rows.items():
        if k not in keys:
            errs.append('%s:%d rank %d: survey row %r matches nothing in the file' % (path, k[0], k[1], r['old']))
    return errs


def rewrite(body, cands, rows):
    """The body with the `auto` and `hand` rows applied (one event per candidate)."""
    out, pos = [], 0
    n = 0
    for c in sorted(cands, key=lambda c: c.start):
        r = rows[(c.line, c.rank)]
        if r['class'] == 'kept':
            continue
        out.append(body[pos:c.start])
        out.append(r['new'])
        pos = c.end
        n += 1
    out.append(body[pos:])
    return ''.join(out), n
