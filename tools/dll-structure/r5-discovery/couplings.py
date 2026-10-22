"""R5 discovery step 3: the couplings a move or a rename of the test files would break (read-only).
Writes couplings.json (list of dicts) and couplings.tsv.

Classes:
  Q1  namespace-qualified reference to a test type (using static Alundra.Tests.X, Alundra.Tests.UI.Y.Member)
  L1  string literal in code that contains an old (Alundra-prefixed) test type name, exactly or as a fragment (temp folder prefixes, messages)
  L2  string literal that contains a path or namespace of the test project (Alundra.Tests, Data/, a test file name)
  N1  nameof()/typeof() of an Alundra-prefixed test type used where its text reaches a runtime value (the value follows the rename)
  R1  runtime reflection on the test namespace (DLL probes testing Namespace.StartsWith("Alundra.Tests"))
  X1  xunit collection names / definitions
  F1  file-system reads that start from a test file location or the test project folder
  C1  comments/docs in the DLL, in tools and in living docs that quote a test path or class (informational, stale after the move)
  T1  tools/dll-structure scripts or data keyed by test paths (r3-literals.tsv, prove_r3, ...)
"""
import json
import re
import sys

from common import *  # noqa: F401,F403

sys.stdout.reconfigure(encoding='utf-8')

tfs = [TestFile(r) for r in test_files() if r.endswith('.cs')]
old_top = {}  # old name -> file
for t in tfs:
    for d in t.top:
        old_top[d['name']] = t.rel
pref_names = {n: f for n, f in old_top.items() if n.startswith('Alundra') or n.startswith('IAlundra')}
file_stems = {t.base[:-3] for t in tfs}

rows = []


def add(cls, where, line, text, token, effect, action):
    rows.append(dict(cls=cls, where=where, line=line, text=text.strip()[:240], token=token, effect=effect, action=action))


def line_of(text, off):
    return text.count('\n', 0, off) + 1


ALUNDRA_TOKEN = re.compile(r'Alundra[A-Za-z0-9]*')

# ---------------------------------------------------------------- L1 / L2: string literals of the tests, the DLL, the tools
SOURCES = [t for t in tfs]
extra = []
for rel in git_lines('ls-files', '--', 'Alundra', 'tools/AlundraTestSaves', 'alundra-casaengine-project-converter', 'alundra-casaengine-project-converter.Tests'):
    if rel.endswith('.cs') and '/obj/' not in rel and '/bin/' not in rel:
        extra.append(TestFile(rel))
SOURCES += extra

for t in SOURCES:
    for a, b, k in t.src.spans:
        if k != 's':
            continue
        lit = t.text[a:b]
        for m in ALUNDRA_TOKEN.finditer(lit):
            tok = m.group(0)
            if tok in pref_names:
                add('L1', t.rel, line_of(t.text, a), lit, tok, 'exact old test type name inside a string: follows the rename only if edited', 'edit with the rename')
                continue
            frag = [n for n in pref_names if n.startswith(tok) and len(tok) >= 9]
            if frag and tok not in ('AlundraCells', 'AlundraWallPlacements', 'AlundraFloorPlacements', 'AlundraCompanionJson'):
                add('L1', t.rel, line_of(t.text, a), lit, tok, 'fragment of old test type name(s) %s: temp-folder / message prefix' % ', '.join(sorted(frag)[:3]), 'edit with the rename')
                continue
            # a longer token that starts with an old test type name (e.g. AlundraSoundPlayerTests_ + guid is split at '_' so exact)
        if 'Alundra.Tests' in lit or re.search(r'(Tests?/|Alundra\.Tests[/\\])', lit) and 'Alundra.Tests' in lit:
            add('L2', t.rel, line_of(t.text, a), lit, 'Alundra.Tests', 'path or namespace of the test project in a string', 'check')
        if re.search(r'\bAlundra[A-Za-z0-9]*(Tests?|Support|Oracle|Harness)\.cs\b', lit):
            add('L2', t.rel, line_of(t.text, a), lit, 'test file name', 'test file name in a string', 'edit with the move')
        if '"Data"' == lit or 'story-chain-skipped-opcodes' in lit:
            add('F1', t.rel, line_of(t.text, a), lit, lit[:60], 'reads a file kept in the project folder of the tests (not the exported Data/)' if 'story-chain' in lit else 'exported Data/ folder of alundra-project (unrelated to Alundra.Tests/Data)',
                'path follows only if the .tsv moves' if 'story-chain' in lit else 'none')


# ---------------------------------------------------------------- Q1: namespace-qualified references to test types (using static, using, inline)
for t in tfs:
    m_ = t.masked
    for m in re.finditer(r'Alundra\s*\.\s*Tests\b', m_):
        ls = m_.rfind(chr(10), 0, m.start()) + 1
        le = m_.find(chr(10), m.start())
        text = t.text[ls:le].strip()
        if text.startswith('namespace '):
            continue
        add('Q1', t.rel, line_of(t.text, m.start()), text, 'Alundra.Tests.<...>', 'qualified reference to a test type/namespace: must follow the new namespace (and the new name)', 'edit with the move')

# ---------------------------------------------------------------- N1: nameof/typeof of a prefixed test type, GetType().Name in tests
for t in tfs:
    m_ = t.masked
    for m in re.finditer(r'\b(nameof|typeof)\s*\(\s*(?:[\w.]+\.)?(\w+)\s*\)', m_):
        if m.group(2) in pref_names:
            tail = m_[m.end():m.end() + 30]
            add('N1', t.rel, line_of(t.text, m.start()), m_[m.start():m.end() + 25], m.group(2), 'value derived at runtime from the test type name (follows the rename; changes the value written)', 'none (value changes)')
    for m in re.finditer(r'GetType\(\)\s*\.\s*(Name|FullName)|MethodBase\.GetCurrentMethod|CallerMemberName|CallerFilePath|\bStackTrace\b|\.Namespace\b', m_):
        add('N1', t.rel, line_of(t.text, m.start()), m_[max(0, m.start() - 40):m.end() + 30], m.group(0), 'reflection on the test type / call site', 'review')

# ---------------------------------------------------------------- R1: DLL code that reads the test namespace
for t in extra:
    if t.rel.startswith('Alundra/'):
        for m in re.finditer(r'Namespace', t.text):
            ctx = t.text[m.start() - 60:m.end() + 90]
            if 'Alundra.Tests' in ctx:
                add('R1', t.rel, line_of(t.text, m.start()), ctx.replace('\n', ' '), 'StartsWith("Alundra.Tests")', 'prefix test: still true for Alundra.Tests.<Domain>; the logged test id is outer.Name + "." + method (simple class name: changes with the rename)', 'none (log text changes)')

# ---------------------------------------------------------------- X1: xunit collection names and definitions
for t in tfs:
    for m in re.finditer(r'\[CollectionDefinition\(([^)]*)\)\]', t.masked):
        add('X1', t.rel, line_of(t.text, m.start()), t.masked[m.start():m.end()], m.group(1), 'collection definition (Name const in the class below)', 'none if the class keeps the Name const')
    for m in re.finditer(r'(public|internal)\s+const\s+string\s+Name\s*=\s*("(?:[^"\\]|\\.)*")', t.text):
        add('X1', t.rel, line_of(t.text, m.start()), m.group(0), m.group(2), 'xunit collection name string (kept by R3 as class 4 literal)', 'none (string kept)')

# ---------------------------------------------------------------- C1: comments in the DLL / docs that quote a test path or class
for t in extra:
    if not t.rel.startswith('Alundra/'):
        continue
    for a, b, k in t.src.spans:
        if k == 'c':
            c = t.text[a:b]
            for m in re.finditer(r'Alundra\.Tests[/\\][\w./\\-]+|Alundra[A-Za-z0-9]+(?:Tests|Support|Oracle|Harness)\b|IntroTraceHarnessTests|HeroTraceHarnessTests', c):
                add('C1', t.rel, line_of(t.text, a), c.replace('\n', ' '), m.group(0), 'comment quotes a test path or class: stale after the move/rename (informational)', 'optional edit')

# ---------------------------------------------------------------- T1: tools keyed by test paths
for rel in git_lines('ls-files', '--', 'tools'):
    if rel.endswith('.tsv') or rel.endswith('.py') or rel.endswith('.cs') or rel.endswith('.md'):
        if '/obj/' in rel or '/bin/' in rel:
            continue
        txt = read_text(rel)
        n = len(re.findall(r'Alundra\.Tests/[\w./-]+\.cs', txt))
        if n:
            add('T1', rel, 0, '%d reference(s) to Alundra.Tests/<file>.cs paths' % n, 'Alundra.Tests/*.cs', 'keyed by the current test file paths: breaks when the files move' if rel.endswith('.tsv') else 'mentions', 'rewrite or freeze' if rel.endswith('.tsv') else 'none')

json.dump(rows, open(WORK + '/couplings.json', 'w', encoding='utf-8'), indent=1)
with open(WORK + '/couplings.tsv', 'w', encoding='utf-8', newline='') as f:
    f.write('class\twhere\tline\ttoken\teffect\taction\ttext\n')
    for r in rows:
        f.write('\t'.join([r['cls'], r['where'], str(r['line']), r['token'], r['effect'], r['action'], r['text'].replace('\t', ' ').replace('\n', ' ')]) + '\n')
import collections
print(collections.Counter(r['cls'] for r in rows))
