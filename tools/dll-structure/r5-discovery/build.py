"""R5 discovery step 4: builds r5-files.tsv (one row per file of Alundra.Tests after R5, new files of the cuts included), r5-types.tsv (one row per
top-level type), r5-folders.tsv and checks.json. Read-only on the repository.

Run order: (il_refs.tsv from ilscan/) -> evidence.py -> couplings.py -> build.py -> extras.py   (outputs go to $R5_WORK)

Rules applied (D-R-13, D-R-16, D-R-19, ADR-0080; P-R5-1 of docs/plan-restructuration-dll.md section 9): see decisions.py for the folder of a file
and `plan_file` below for the split of the files that hold several types.
"""
import collections
import json
import re
import sys

from common import *  # noqa: F401,F403
from decisions import STRUCTURAL, SUBJECT

sys.stdout.reconfigure(encoding='utf-8')

DOMAINS = ['Support', 'Maps', 'Session', 'Entities', 'Events', 'Ai', 'Hero', 'Combat', 'Items', 'Rng', 'Physics', 'Cells', 'Cameras',
           'Rendering', 'Audio', 'Transitions', 'Dialogues', 'UI', 'SaveGames', 'Probes', 'ExportGuards', 'Arcs']

table = load_table()
cur, name_idx, unmatched, dll_paths = dll_world(table)
D = json.load(open(WORK + '/evidence.json', encoding='utf-8'))
ev, df, N = D['ev'], D['df'], D['N']
tfs = {r: TestFile(r) for r in test_files() if r.endswith('.cs')}
all_files = test_files()
PFX = 'Alundra.Tests/'


def key(rel):
    return rel[len(PFX):]


def domain_of(folder):
    return folder.split('/')[0]


def idf_top(rel):
    f = collections.Counter(ev[rel]['folder_idf'])
    items = [(k, v) for k, v in f.most_common() if v > 0]
    return items, sum(v for _, v in items)


# ------------------------------------------------------------------------------------------------ 1. the folder of each file
dec = {}
for rel, t in tfs.items():
    e = ev[rel]
    nt, tk = e['name_type'], e['name_tok']
    items, tot = idf_top(rel)
    k = key(rel)
    d = dict(folder=None, basis='', reason='', alt='', flags=[])
    if k in STRUCTURAL:
        f, reason, alt = STRUCTURAL[k]
        d.update(folder=f, basis='structural', reason=reason, alt=alt)
    elif nt:
        d.update(folder=nt[1], basis='name-type', reason='named after the DLL type %s' % nt[0])
    elif tk and len(tk[1]) == 1:
        d.update(folder=tk[1][0], basis='name-word', reason='its first word %s names the folder' % tk[0])
    elif k in SUBJECT:
        f, reason, alt = SUBJECT[k]
        d.update(folder=f, basis='subject', reason=reason, alt=alt)
    elif items:
        d.update(folder=items[0][0], basis='refs', reason='most distinctive references')
    else:
        d.update(folder=None, basis='none')
    dec[rel] = d

# a name that overrides a SUBJECT entry is worth a line (the author's rule wins)
subject_overruled = [(k, SUBJECT[k][0], dec[PFX + k]['folder'], dec[PFX + k]['basis']) for k in SUBJECT
                     if PFX + k in dec and dec[PFX + k]['basis'] in ('name-type', 'name-word') and dec[PFX + k]['folder'] != SUBJECT[k][0]]

by_top = collections.defaultdict(list)
for rel, t in tfs.items():
    for dcl in t.top:
        by_top[(dcl['ns'], dcl['name'])].append(rel)
groups = {k: v for k, v in by_top.items() if len(v) > 1}
group_of = {}
for k, v in groups.items():
    for rel in v:
        group_of[rel] = sorted(v)

# ------------------------------------------------------------------------------------------------ 2. users of each test-defined type
decl_of = collections.defaultdict(set)
for rel, t in tfs.items():
    for dcl in t.top:
        decl_of[dcl['name']].add(rel)
users = collections.defaultdict(lambda: collections.defaultdict(int))        # declaring file -> user file -> refs
type_users = collections.defaultdict(lambda: collections.defaultdict(set))   # declaring file -> type -> user files
for rel in tfs:
    for g, name, c in ev[rel]['t2t']:
        users[g][rel] += c
        type_users[g][name].add(rel)

# ------------------------------------------------------------------------------------------------ 3. helpers (no test): the folder of their users
known = {rel: d['folder'] for rel, d in dec.items() if tfs[rel].n_tests > 0 or key(rel) in STRUCTURAL or key(rel) in SUBJECT}
helpers = [rel for rel in tfs if tfs[rel].n_tests == 0 and key(rel) not in STRUCTURAL and key(rel) not in SUBJECT]
for _ in range(4):
    for rel in helpers:
        fs = collections.Counter(known[u] for u in users[rel] if u in known and known[u] is not None)
        d = dec[rel]
        if not users[rel]:
            d.update(folder=d['folder'] or 'Support', basis='helper-nouser', reason='no other test file references its types')
            d['flags'] = ['HELPER_NOUSER']
        elif not fs:
            continue
        elif len(fs) == 1:
            f = next(iter(fs))
            d.update(folder=f, basis='helper-local', reason='all %d user file(s) are in %s' % (sum(fs.values()), f))
            d['flags'] = ['HELPER_LOCAL']
            known[rel] = f
        else:
            tops = {f.split('/')[0] for f in fs}
            if tops == {'UI'}:
                d.update(folder='UI/Shared', basis='helper-shared', reason='users in %s' % ', '.join('%s:%d' % x for x in fs.most_common()))
            else:
                d.update(folder='Support', basis='helper-shared', reason='users in %d folders: %s' % (len(fs), ', '.join('%s:%d' % x for x in fs.most_common(6))))
            d['flags'] = ['HELPER_SHARED']
            known[rel] = d['folder']
for rel in helpers:
    if dec[rel]['folder'] is None:
        dec[rel].update(folder='Support', basis='helper-unresolved', reason='users unresolved')
        dec[rel]['flags'] = ['HELPER_UNRESOLVED']

# a partial group follows the folder of its primary file
for k, v in groups.items():
    primary = sorted(v, key=lambda r: ('.' in tfs[r].base[:-3], r))[0]
    for rel in v:
        if dec[rel]['folder'] != dec[primary]['folder']:
            dec[rel].update(folder=dec[primary]['folder'], basis=dec[rel]['basis'] + '+group', reason=(dec[rel]['reason'] + ' ' if dec[rel]['reason'] else '') + 'follows %s (partial class)' % tfs[primary].base)

# ------------------------------------------------------------------------------------------------ 4. flags
for rel, t in tfs.items():
    d, e = dec[rel], ev[rel]
    items, tot = idf_top(rel)
    f = d['flags']
    if rel in group_of:
        f.append('PARTIAL_GROUP')
    if items and tot >= 8:
        top1, s1 = items[0]
        if d['folder'] != top1 and s1 / tot >= 0.40:
            f.append('NAME_NE_REFS(%s %d%%)' % (top1, 100 * s1 / tot))
        if len(items) > 1:
            s2 = items[1][1]
            if (s1 - s2) / tot < 0.12 and s2 / tot >= 0.15 and d['folder'] in (items[0][0], items[1][0]):
                f.append('TIE(%s/%s)' % (items[0][0], items[1][0]))
    if items and not d['alt']:
        alts = []
        for fl in f:
            m = re.match(r'NAME_NE_REFS\((.+) \d+%\)', fl)
            if m:
                alts.append(m.group(1))
            m = re.match(r'TIE\((.+)/(.+)\)', fl)
            if m:
                alts.extend(x for x in (m.group(1), m.group(2)) if x != d['folder'])
        d['alt'] = ' or '.join(dict.fromkeys(alts))
    if t.n_tests > 0 and not items and d['basis'] != 'structural':
        f.append('NO_DLL_REF')


def conf(d):
    """high: decided by a name, a structure or its users and no doubt flag; low: decided by the subject or the references."""
    bad = [x.split('(')[0] for x in d['flags'] if x.split('(')[0] in ('TIE', 'NAME_NE_REFS', 'NO_DLL_REF', 'HELPER_NOUSER', 'HELPER_UNRESOLVED')]
    if d['basis'] in ('structural', 'name-type', 'name-word') and d['folder'] == 'Arcs':
        bad = []
    if d['basis'] in ('name-type', 'name-word'):
        bad = [x for x in bad if x != 'TIE']
    if d['basis'] in ('subject', 'refs', 'refs+group', 'subject+group'):
        return 'low' if (d['basis'].startswith('subject') or bad) else 'medium'
    return 'high' if not bad else ('medium' if len(bad) == 1 else 'low')


# ------------------------------------------------------------------------------------------------ 5. P-R5-1: the split of each file
def ns_of(folder):
    return 'Alundra.Tests.' + folder.replace('/', '.')


plan = {}   # rel -> dict(main, rule, cuts: [(type, reason)], new_path, new_stem)
problems = []
for rel, t in sorted(tfs.items()):
    stem = t.base[:-3]
    head, dot, tail = stem.partition('.')
    names = [x['name'] for x in t.top]
    d = dec[rel]
    folder = d['folder'] or 'Support'
    d['folder'] = folder
    if head in names:
        main, rule = head, 'stem'
    else:
        pub = [x for x in t.top if x['access'] == 'public']
        main = (pub[0] if pub else t.top[0])['name']
        rule = 'iii-public' if pub else 'iii-first'
    cuts = []
    for x in t.top:
        if x['name'] == main:
            continue
        shared = bool(type_users[rel].get(x['name']))
        if x['partial']:
            problems.append('%s: the type %s is partial and would be cut' % (rel, x['name']))
        if x['access'] == 'public' or shared:
            cuts.append((x['name'], 'public+shared' if x['access'] == 'public' and shared else ('public' if x['access'] == 'public' else 'shared')))
    new_stem = new_name(main) + dot + tail
    plan[rel] = dict(main=main, rule=rule, cuts=cuts, new_stem=new_stem, new_path='%s%s/%s.cs' % (PFX, folder, new_stem), folder=folder)
    d['new_ns'] = ns_of(folder)

# ------------------------------------------------------------------------------------------------ 6. the rows
file_rows = []   # dicts
type_rows = []
for rel, t in sorted(tfs.items()):
    d, p = dec[rel], plan[rel]
    ren = ';'.join('%s>%s' % (x['name'], new_name(x['name'])) for x in t.top if new_name(x['name']) != x['name'])
    file_rows.append(dict(old_path=rel, new_path=p['new_path'], namespace=d['new_ns'], domain=domain_of(d['folder']), kind='move', source_file='',
                          main_type=p['main'], rule=p['rule'], basis=d['basis'], confidence=conf(d), decision=d['reason'], flags=' '.join(d['flags']),
                          alternatives=d['alt'], types=';'.join('%s:%s' % (x['name'], x['kind']) for x in t.top), tests=t.n_tests, lines=t.lines,
                          cut_types=';'.join(c[0] for c in p['cuts']), partial_group=';'.join(x[len(PFX):] for x in group_of.get(rel, []))))
    cutnames = {c[0]: c[1] for c in p['cuts']}
    for x in t.top:
        nn = new_name(x['name'])
        if x['name'] in cutnames:
            nf = '%s%s/%s.cs' % (PFX, d['folder'], nn)
        else:
            nf = p['new_path']
        type_rows.append(dict(old_file=rel, old_ns=x['ns'], old=x['name'], kind=x['kind'], access=x['access'], partial=x['partial'], new_ns=d['new_ns'], new=nn,
                              old_full='%s.%s' % (x['ns'], x['name']), new_full='%s.%s' % (d['new_ns'], nn), new_file=nf, domain=domain_of(d['folder']),
                              cut=('yes:' + cutnames[x['name']]) if x['name'] in cutnames else 'no', main='yes' if x['name'] == p['main'] else 'no'))
        if x['name'] in cutnames:
            file_rows.append(dict(old_path='', new_path=nf, namespace=d['new_ns'], domain=domain_of(d['folder']), kind='cut', source_file=rel,
                                  main_type=x['name'], rule='cut-' + cutnames[x['name']], basis=d['basis'], confidence='high',
                                  decision='type %s cut from %s (P-R5-1 %s)' % (x['name'], t.base, cutnames[x['name']]), flags='', alternatives='',
                                  types='%s:%s' % (x['name'], x['kind']), tests='', lines='', cut_types='', partial_group=''))
for p in all_files:
    if p.endswith('.cs'):
        continue
    kind = 'csproj' if p.endswith('.csproj') else ('json' if p.endswith('.json') else 'data')
    why = {'csproj': 'Alundra.Tests.csproj: no Compile/Content/Link items (SDK globbing), RootNamespace Alundra.Tests, one None item (xunit.runner.json); nothing to edit',
           'json': 'xunit.runner.json: parallelizeTestCollections=false; no per-class or per-namespace setting; nothing to edit',
           'data': 'D-R-13 point 6: stays at Alundra.Tests/Data; read by path by AlundraStoryChainOpcodeAudit.FindListPath; its literal does not change'}[kind]
    file_rows.append(dict(old_path=p, new_path=p, namespace='', domain='', kind='keep', source_file='', main_type='', rule='', basis='keep', confidence='high',
                          decision=why, flags='', alternatives='', types='', tests='', lines='', cut_types='', partial_group=''))

# ------------------------------------------------------------------------------------------------ 7. checks
cs_rows = [r for r in file_rows if r['kind'] in ('move', 'cut')]
path_count = collections.Counter(r['new_path'].lower() for r in file_rows)
dup_paths = {p: c for p, c in path_count.items() if c > 1}
full_count = collections.Counter(r['new_full'] for r in type_rows)
dup_types = {p: c for p, c in full_count.items() if c > 1 and not any(r['partial'] for r in type_rows if r['new_full'] == p)}
covered = collections.Counter(r['old_path'] for r in file_rows if r['kind'] in ('move', 'keep'))
missing = [p for p in all_files if covered[p] != 1]
type_once = collections.Counter(r['old_full'] for r in type_rows)
type_dup_old = {k: c for k, c in type_once.items() if c > 1 and not any(r['partial'] for r in type_rows if r['old_full'] == k)}

ext = scan_types.external_types()
dll_names_final = {v['final_name'] for v in cur.values()}
dll_names_cur = {v['cur_name'] for v in cur.values()}
dll_segs = dll_segments(cur)
test_segs = {seg for r in cs_rows for seg in r['namespace'].split('.')}
coll = []
for r in type_rows:
    nn = r['new']
    if nn in dll_names_final or nn in dll_names_cur:
        coll.append('[dll] %s equals a DLL type' % r['new_full'])
    for e in ext.get(nn.lower(), []):
        if not e['nested']:
            coll.append('[ext] %s collides with %s %s.%s' % (r['new_full'], e['label'], e['ns'], e['name']))
    if nn in dll_segs | test_segs:
        coll.append('[segment] %s equals a namespace segment' % r['new_full'])
synthetic = [('r5-namespace', 'Alundra.Tests.' + seg, '_r5_') for seg in sorted(test_segs - {'Alundra', 'Tests'})]
synthetic += [(w, v['final_ns'], v['final_name']) for w, v in cur.items()]
synthetic += [(r['old_file'], r['new_ns'], r['new']) for r in type_rows]
cn_findings = check_names.collision_findings(synthetic, ext)
nested_warn = []
for rel, t in tfs.items():
    for dcl in t.nested:
        nm = dcl['name']
        if nm in dll_segs | test_segs:
            nested_warn.append((rel, nm, 'nested type named like a namespace segment'))
        if nm in dll_names_final or nm in dll_names_cur:
            nested_warn.append((rel, nm, 'nested type named like a DLL type'))
domains_present = {r['domain'] for r in cs_rows}
bad_domain = sorted(domains_present - set(DOMAINS))

# ------------------------------------------------------------------------------------------------ 8. outputs
COLS_F = ['old_path', 'new_path', 'namespace', 'domain', 'kind', 'source_file', 'main_type', 'rule', 'basis', 'confidence', 'decision', 'flags',
          'alternatives', 'types', 'tests', 'lines', 'cut_types', 'partial_group']


def order_key(r):
    return (DOMAINS.index(r['domain']) if r['domain'] in DOMAINS else 99, r['new_path'].lower())


with open(WORK + '/r5-files.tsv', 'w', encoding='utf-8', newline='') as f:
    f.write('\t'.join(COLS_F) + '\n')
    for r in sorted(file_rows, key=order_key):
        f.write('\t'.join(str(r[c]).replace('\t', ' ').replace('\n', ' ') for c in COLS_F) + '\n')

COLS_T = ['old_file', 'old_namespace', 'old_type', 'kind', 'access', 'partial', 'new_namespace', 'new_type', 'old_full', 'new_full', 'new_file', 'domain', 'main', 'cut']
KEYS_T = ['old_file', 'old_ns', 'old', 'kind', 'access', 'partial', 'new_ns', 'new', 'old_full', 'new_full', 'new_file', 'domain', 'main', 'cut']
with open(WORK + '/r5-types.tsv', 'w', encoding='utf-8', newline='') as f:
    f.write('\t'.join(COLS_T) + '\n')
    for r in sorted(type_rows, key=lambda r: (DOMAINS.index(r['domain']), r['old_file'], r['old'])):
        f.write('\t'.join(str(r[k]) for k in KEYS_T) + '\n')

cnt = collections.Counter(r['new_path'].rsplit('/', 1)[0][len(PFX):] for r in cs_rows)
with open(WORK + '/r5-folders.tsv', 'w', encoding='utf-8', newline='') as f:
    f.write('folder\tnamespace\tfiles\n')
    for k in sorted(cnt):
        f.write('%s\t%s\t%d\n' % (k, ns_of(k), cnt[k]))

json.dump(dict(dup_paths=dup_paths, dup_types=dup_types, missing=missing, type_dup_old=type_dup_old, coll=coll, cn_findings=cn_findings,
               nested_warn=nested_warn, problems=problems, bad_domain=bad_domain,
               subject_overruled=subject_overruled,
               dec={k: dict(folder=v['folder'], basis=v['basis'], reason=v['reason'], alt=v['alt'], flags=v['flags'], conf=conf(v)) for k, v in dec.items()},
               plan={k: dict(main=v['main'], rule=v['rule'], cuts=v['cuts'], new_path=v['new_path']) for k, v in plan.items()}),
          open(WORK + '/checks.json', 'w', encoding='utf-8'), indent=1)

print('cs files', len(tfs), '; rows', len(file_rows), '(cut files %d)' % sum(1 for r in file_rows if r['kind'] == 'cut'), '; types', len(type_rows))
print('basis', dict(collections.Counter(d['basis'] for d in dec.values())))
print('rules', dict(collections.Counter(p['rule'] for p in plan.values())))
for k in sorted(cnt):
    print('  %-24s %3d' % (k, cnt[k]))
print('dup paths', dup_paths, '| dup types', dup_types, '| not covered once', missing, '| dup old types', type_dup_old)
print('collisions', len(coll), len(cn_findings), '| problems', problems, '| bad domain', bad_domain)
print('subject entries overruled by a name:', subject_overruled)
