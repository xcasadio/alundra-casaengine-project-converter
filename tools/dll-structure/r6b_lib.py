"""R6b library: the line citations of the 21 living documents, and the cited lines at the pinned trees.

Shared by apply_r6b.py (writes the survey and the documents) and prove_r6b.py (checks them).  Read-only on the repository.

Conventions
- BASE_COMMIT is the start of the slice; the documents are read from its blobs (LF), the working tree is CRLF.
- A citation is `File.cs:a-b`, a bare `:a-b` (the file of the sentence), or the prose `ligne N`; a *range* is one number or one
  a-b inside it (`:287,313-321` = 1 citation, 2 ranges).
- The code cited is never changed: decompilation and engine are read at the pointers recorded by BASE_COMMIT (git show, not the
  working tree of the submodule, which carries the author's own changes).
"""
import re
import subprocess
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
BASE_COMMIT = '33528d55'

DOCS = [
    'docs/census-0x24-waits.md', 'docs/editeur-couverture-dll.md',
    'docs/formats/README.md', 'docs/formats/audio-manifests.md', 'docs/formats/backdrops.md', 'docs/formats/cells-companion.md',
    'docs/formats/dialogues-yarn.md', 'docs/formats/effects.md', 'docs/formats/events.md', 'docs/formats/font.md',
    'docs/formats/map-sprite-indices.md', 'docs/formats/misc-data.md', 'docs/formats/save-game.md',
    'docs/formats/sprite-records.md', 'docs/formats/world-index.md',
    'docs/guidelines-runtime-alundra-casaengine.md', 'docs/intro-roadmap.md', 'docs/stick-probe.md', 'docs/test-saves.md',
    'README.md', 'research/census/README.md',
]

# JSON literals ({"X1":0,...}) that the bare-number pattern matches: not citations.
NOT_CITATIONS = {('docs/formats/effects.md', 37), ('docs/formats/effects.md', 46)}

SPEC = r'\d+(?:\s?(?:-|–|\.\.)\s?\d+)?(?:\s?,\s?:?\d+(?:\s?(?:-|–|\.\.)\s?\d+)?)*'
EXT = r'(?:cs|py|md|json|csproj|ps1|tsv|txt|sh|xaml|slnx|yml|props|targets|c|h|html|js|mjs|cmd)'
RE_EXPLICIT = re.compile(r'(?P<path>(?:[\w.\-]+/)*[\w.\-]*[\w\-]\.' + EXT + r'):(?P<spec>' + SPEC + r')(?![\w])')
RE_BARE = re.compile(r'(?<![\w.\)\]/\-])(?P<bare>:' + SPEC + r')(?![\w])')
RE_PROSE = re.compile(r'(?P<prose>\b(?:lignes?|l\.|lines?|ll\.|L\.)\s?' + SPEC + r')', re.I)
RE_RANGE = re.compile(r'(\d+)(?:\s?(?:-|–|\.\.)\s?(\d+))?')

AE = 'alundra-datas-analyser/AlundraTools/AlundraEngine/'
BY_NAME = {
    'GameEngine.cs': AE + 'GameEngine.cs',
    'EntityManager.cs': AE + 'Gameplay/EntityManager.cs',
    'EntityEventHandlers.cs': AE + 'Gameplay/Scripts/EntityEventHandlers.cs',
    'SpriteEventHandlers.cs': AE + 'Gameplay/Scripts/SpriteEventHandlers.cs',
    'FunctionTypeA.cs': AE + 'Gameplay/Scripts/FunctionTypeA.cs',
    'FunctionTypeE.cs': AE + 'Gameplay/Scripts/FunctionTypeE.cs',
    'GameInitializer.cs': AE + 'GameInitializer.cs',
    'PlayerManager.cs': AE + 'Gameplay/PlayerManager.cs',   # the decompilation (homonym of Alundra/Hero/PlayerManager.cs)
    'StaticVariables.cs': AE + 'StaticVariables.cs',
    'PlayerControlFlags.cs': AE + 'PlayerControlFlags.cs',
    'PhysicsEngine.cs': AE + 'PhysicsEngine.cs',
    'TiledMapExporter.cs': 'alundra-datas-analyser/AlundraTools/AlundraDataExtractor/TiledMapExporter.cs',
    'GraphicManager.cs': AE + 'Graphics/GraphicManager.cs',
    'SimulationSpacePolicy.cs': 'CasaEngineMonogame/CasaEngine/Engine/Physics/SimulationSpacePolicy.cs',
    'GameState.cs': 'Alundra/Session/GameState.cs',          # the DLL (the decompilation one has 7 lines)
}
# Bare ':a-b' of docs/intro-roadmap.md: the file of the sentence (decided by reading each line, discovery of 2026-10-22).
BARE_FILE = {43: 'EntityEventHandlers.cs', 60: 'EntityEventHandlers.cs', 61: 'EntityEventHandlers.cs', 79: 'EntityEventHandlers.cs',
             186: 'EntityEventHandlers.cs', 188: 'EntityEventHandlers.cs', 189: 'EntityEventHandlers.cs', 197: 'GameEngine.cs',
             213: 'GameEngine.cs', 214: 'GameEngine.cs', 215: 'GameEngine.cs', 217: 'GameEngine.cs', 225: 'GameEngine.cs',
             261: 'EntityEventHandlers.cs'}
for _l in range(272, 291):
    BARE_FILE[_l] = 'EntityEventHandlers.cs'
# the lines 277-290 are in the GameEngine.cs part of the table (see survey): overridden below by the survey of each sentence
SUBMODULES = ('alundra-datas-analyser', 'CasaEngineMonogame')


def git(*args, cwd=ROOT, raw=False):
    r = subprocess.run(['git', '-C', str(cwd)] + list(args), capture_output=True)
    if r.returncode != 0:
        return None
    return r.stdout if raw else r.stdout.decode('utf-8', 'replace')


def text_at(commit, path):
    b = git('show', f'{commit}:{path}', raw=True)
    if b is None:
        return None
    return b.decode('utf-8-sig').replace('\r\n', '\n')


_pins = {}


def pointer(sub, commit=BASE_COMMIT):
    if (sub, commit) not in _pins:
        out = git('ls-tree', commit, sub) or ''
        m = re.match(r'160000 commit (\w+)', out)
        _pins[(sub, commit)] = m.group(1) if m else None
    return _pins[(sub, commit)]


_cache = {}


def target_text(target, commit=BASE_COMMIT):
    """Text (LF) of a cited file: submodule files at the pointer of `commit`, parent files at `commit`."""
    key = (target, commit)
    if key not in _cache:
        sub = next((s for s in SUBMODULES if target.startswith(s + '/')), None)
        if sub:
            b = git('show', f'{pointer(sub, commit)}:{target[len(sub) + 1:]}', cwd=ROOT / sub, raw=True)
            _cache[key] = None if b is None else b.decode('utf-8-sig', 'replace').replace('\r\n', '\n')
        else:
            _cache[key] = text_at(commit, target)
    return _cache[key]


def target_lines(target, commit=BASE_COMMIT):
    t = target_text(target, commit)
    return None if t is None else t.split('\n')


def extract_all(commit=BASE_COMMIT):
    """Every range of every citation of the 21 documents, in document/line/column order."""
    out = []
    for doc in DOCS:
        text = text_at(commit, doc)
        last_file = ''
        for ln, line in enumerate(text.split('\n'), 1):
            if (doc, ln) in NOT_CITATIONS:
                continue
            found = []
            for m in RE_EXPLICIT.finditer(line):
                found.append(('explicit', m.start('spec'), m.end('spec'), m.group('path'), m.group('spec')))
            spans = [(a, b) for _, a, b, _, _ in found]
            for m in RE_BARE.finditer(line):
                if any(a <= m.start() < b for a, b in spans):
                    continue
                found.append(('bare', m.start('bare') + 1, m.end('bare'), '', m.group('bare')[1:]))
            for m in RE_PROSE.finditer(line):
                if any(a <= m.start() < b for a, b in spans):
                    continue
                s = m.group('prose')
                k = re.search(SPEC, s).start()
                found.append(('prose', m.start('prose') + k, m.end('prose'), '', s[k:]))
            found.sort(key=lambda x: x[1])
            k = 0
            for kind, a, b, path, spec in found:
                if kind == 'explicit':
                    last_file = path
                # the file of the citation
                cited = path if kind == 'explicit' else ''
                implied = cited
                if kind != 'explicit':
                    implied = BARE_FILE.get(ln, '') if doc == 'docs/intro-roadmap.md' else ''
                    if kind == 'prose' and doc == 'docs/formats/cells-companion.md':
                        implied = 'TiledMapExporter.cs'
                    if doc == 'docs/intro-roadmap.md' and ln in (277, 278, 279, 280, 281, 282, 283, 284, 285, 286, 287, 288, 289, 290):
                        implied = BARE_FILE[ln]
                target = resolve_target(doc, ln, implied)
                for i, rm in enumerate(RE_RANGE.finditer(spec)):
                    lo = int(rm.group(1)); hi = int(rm.group(2) or rm.group(1))
                    out.append(dict(doc=doc, line=ln, k=k, ord=i, kind=kind, cited=implied or cited, before=rm.group(0),
                                    lo=lo, hi=hi, col=a + rm.start(), end=a + rm.end(), text=line, target=target))
                    k += 1
    return out


def resolve_target(doc, ln, token):
    if not token:
        return ''
    if token.startswith('docs/'):
        return token
    if token.startswith('CasaEngineMonogame/'):
        return token
    if token.startswith('AlundraTools/'):
        return 'alundra-datas-analyser/' + token
    return BY_NAME.get(token.split('/')[-1], '')


# ------------------------------------------------------------------------------------------------------------------ members
DECL = re.compile(r'^\s*(?:(?:public|private|internal|protected|static|override|virtual|unsafe)\s+)+[\w<>\[\],.? ]+?\s+(\w+)\s*\(')


def declarations(lines, name):
    """Line numbers (1-based) of the declarations of the method `name` (a signature line that does not end with `;`)."""
    out = []
    for i, s in enumerate(lines, 1):
        m = DECL.match(s)
        if m and m.group(1) == name and not s.rstrip().endswith(';'):
            out.append(i)
    return out


def close_of(lines, decl):
    """Line of the closing brace of the member declared at `decl` (braces in strings and after `//` ignored)."""
    depth = 0
    seen = False
    for i in range(decl, len(lines) + 1):
        s = re.sub(r'"(?:[^"\\]|\\.)*"', '""', lines[i - 1])
        s = re.sub(r"'(?:[^'\\]|\\.)'", "''", s).split('//')[0]
        for ch in s:
            if ch == '{':
                depth += 1
                seen = True
            elif ch == '}':
                depth -= 1
                if seen and depth == 0:
                    return i
    return None


def member_span(lines, name, near):
    """(decl, close) of the declaration of `name` nearest to line `near`, or None."""
    cand = declarations(lines, name)
    if not cand:
        return None
    d = min(cand, key=lambda x: abs(x - near))
    return d, close_of(lines, d)


def probe(lines, n):
    """The text of line n, trimmed, as written in the survey."""
    if n < 1 or n > len(lines):
        return '<beyond the end>'
    return lines[n - 1].strip().replace('\t', ' ').replace('\r', '')[:100]
