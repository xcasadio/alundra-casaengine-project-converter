"""R5 discovery step 1: evidence per test file (IL references + lexical references to DLL types, test-to-test references, name hints).
Writes evidence.json next to this script. Read-only on the repository."""
import json
import math
import re
import sys

from common import *  # noqa: F401,F403

sys.stdout.reconfigure(encoding='utf-8')

table = load_table()
cur, name_idx, unmatched, dll_paths = dll_world(table)
segments = dll_segments(cur)
tfs = [TestFile(r) for r in test_files() if r.endswith('.cs')]
by_rel = {t.rel: t for t in tfs}

# ---- test full name -> files
test_full = collections.defaultdict(list)
for t in tfs:
    for d in t.top:
        test_full['%s.%s' % (d['ns'], d['name'])].append(t.rel)

# ---- IL references (per DLL top-level type), from the compiled Alundra.Tests.dll (copy of bin/Debug)
il = collections.defaultdict(lambda: collections.defaultdict(lambda: [0, 0, 0, 0]))  # file -> dll full -> [calls,news,fields,types]
il_unmapped_owner = set()
il_unknown_dll = collections.Counter()
for line in open(HERE + '/il_refs.tsv', encoding='utf-8'):
    owner, dll, c, n, f, ty = line.rstrip('\n').split('\t')
    owner = re.sub(r'`\d+$', '', owner)
    files = test_full.get(owner)
    if not files:
        il_unmapped_owner.add(owner)
        continue
    if dll not in cur:
        il_unknown_dll[dll] += 1
    for rel in files:
        a = il[rel][dll]
        a[0] += int(c); a[1] += int(n); a[2] += int(f); a[3] += int(ty)

# ---- lexical references to DLL type names
lex = collections.defaultdict(lambda: collections.Counter())  # file -> dll full -> count
for t in tfs:
    for off, name, q in t.idents():
        fulls = name_idx.get(name)
        if not fulls:
            continue
        if t.is_directive_line(off):
            continue
        if q is not None and q not in segments and q != 'Alundra':
            continue  # member access on an expression
        for full in fulls:
            lex[t.rel][full] += 1

# ---- test-to-test references (top-level test types declared in another file)
top_decl = collections.defaultdict(set)  # simple name -> files declaring it as top-level
for t in tfs:
    for d in t.top:
        top_decl[d['name']].add(t.rel)
t2t = collections.defaultdict(lambda: collections.Counter())  # file -> (declaring file, type) -> count
USING_STATIC = re.compile(r'^[ 	]*using\s+static\s+([\w.]+)\s*;', re.M)
for t in tfs:
    own = {d['name'] for d in t.decls}
    # `using static Alundra.Tests.X;` is a reference to the test type X (its static members are then used by simple name)
    for m in USING_STATIC.finditer(t.masked):
        last = m.group(1).split('.')[-1]
        for g in top_decl.get(last, ()):
            if g != t.rel:
                t2t[t.rel][(g, last)] += 1
    for off, name, q in t.idents():
        decl = top_decl.get(name)
        if not decl or t.is_directive_line(off):
            continue
        if q is not None and q not in ('Tests', 'UI', 'Scripts', 'Alundra'):
            continue
        for g in decl:
            if g != t.rel:
                t2t[t.rel][(g, name)] += 1

# ---- document frequency for the idf weights
N = len(tfs)
df = collections.Counter()
for t in tfs:
    seen = set(il[t.rel]) | set(lex[t.rel])
    for full in seen:
        df[full] += 1


def weight(full):
    return 1.0 + math.log(N / max(1, df[full]))


def folder_scores(rel, use_idf=True):
    s = collections.Counter()
    for full in set(il[rel]) | set(lex[rel]):
        if full not in cur:
            continue
        a = il[rel].get(full, [0, 0, 0, 0])
        c = sum(a) + lex[rel].get(full, 0)
        s[cur[full]['final_folder']] += c * (weight(full) if use_idf else 1.0)
    return s


def type_scores(rel):
    s = collections.Counter()
    for full in set(il[rel]) | set(lex[rel]):
        if full not in cur:
            continue
        a = il[rel].get(full, [0, 0, 0, 0])
        s[cur[full]['final_name']] += sum(a) + lex[rel].get(full, 0)
    return s


# ---- name hint
FOLDERS = sorted({v['final_folder'] for v in cur.values() if v['final_folder']})


def singular(w):
    w = w.lower()
    if w.endswith('ies'):
        return w[:-3] + 'y'
    if w.endswith('s') and not w.endswith('ss'):
        return w[:-1]
    return w


TOKEN_FOLDER = {}
for f in FOLDERS:
    seg = f.split('/')[-1]
    TOKEN_FOLDER.setdefault(singular(seg), set()).add(f)
    TOKEN_FOLDER.setdefault(seg.lower(), set()).add(f)
# a few folder words that the file names use in the other form
for tok, f in (('camera', 'Cameras'), ('cell', 'Cells'), ('dialogue', 'Dialogues'), ('savegame', 'SaveGames'), ('save', 'SaveGames'), ('sound', 'Audio'),
               ('music', 'Audio'), ('bgm', 'Audio'), ('sfx', 'Audio'), ('random', 'Rng'), ('rng', 'Rng'), ('hud', 'UI/Hud'), ('backdrop', 'Rendering/Backdrops'),
               ('effect', 'Rendering/Effects'), ('warp', 'Transitions'), ('portal', 'Transitions'), ('screenfade', 'Transitions')):
    TOKEN_FOLDER.setdefault(tok, set()).add(f)


def stem_of(base):
    s = re.sub(r'\.cs$', '', base)
    s = re.sub(r'\.(Rolls|Advance)$', '', s)
    s = new_name(s)
    s = re.sub(r'(Tests?|Support|Oracle|Harness|Fixture|Montage|Probe|Audit|Samples|Checks|Rig|Arena)$', '', s)
    return s


def camel_tokens(s):
    return re.findall(r'[A-Z][a-z0-9]*|[0-9]+', s)


def name_hint(base):
    stem = stem_of(base)
    best = None
    for full, v in cur.items():
        n = v['final_name']
        if stem.startswith(n) and len(n) >= 4 and (len(stem) == len(n) or stem[len(n)].isupper()):
            if best is None or len(n) > len(best[0]):
                best = (n, v['final_folder'])
    toks = camel_tokens(stem)
    tokhint = None
    for k in (3, 2, 1):
        key = ''.join(toks[:k]).lower()
        if key and key in TOKEN_FOLDER:
            tokhint = (''.join(toks[:k]), sorted(TOKEN_FOLDER[key]))
            break
    return best, tokhint


ev = {}
for t in tfs:
    rel = t.rel
    fs_idf = folder_scores(rel, True)
    fs_raw = folder_scores(rel, False)
    ts = type_scores(rel)
    nh = name_hint(t.base)
    ev[rel] = dict(
        ns=t.ns, nss=t.nss, lines=t.lines, n_tests=t.n_tests,
        top=[dict(name=d['name'], kind=d['kind'], access=d['access'], partial=d['partial']) for d in t.top],
        nested=[d['name'] for d in t.nested],
        il={full: v for full, v in il[rel].items()},
        lex={full: v for full, v in lex[rel].items()},
        folder_idf=dict(fs_idf), folder_raw=dict(fs_raw), types=dict(ts),
        name_type=nh[0], name_tok=nh[1],
        t2t=[[g, n, c] for (g, n), c in t2t[rel].items()],
        usings=t.usings,
    )

json.dump(dict(ev=ev, df=dict(df), N=N, il_unmapped=sorted(il_unmapped_owner), il_unknown=dict(il_unknown_dll), unmatched=[d['name'] for d in unmatched],
               folders=FOLDERS), open(WORK + '/evidence.json', 'w', encoding='utf-8'), indent=1)
print('files', N, 'il_unmapped owners', len(il_unmapped_owner), sorted(il_unmapped_owner)[:10], 'il_unknown', dict(il_unknown_dll))
print('folders', FOLDERS)
