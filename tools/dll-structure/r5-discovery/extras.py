"""R5 discovery step 5: the surveys that the tools apply and prove, in their values of BEFORE R5 (read-only on the repository).

  r5-literals.tsv    L1: the string literals that carry an old (Alundra-prefixed) test class name
                     columns file, line, class, old, new, rank, domain, text      (rank: n-th occurrence of `old` in the strings of `file`)
  r5-qualified.tsv   Q1 (a qualified reference to a test type that moves: `using static Alundra.Tests.X;`, `Alundra.Tests.UI.TextBoxGpu.Device`,
                     `using Alundra.Tests.UI;`) and capture (a partially qualified DLL name such as `Items.ItemTables` that binds to the new test
                     namespace `Alundra.Tests.Items` once it exists)
                     columns file, kind, text, rank, domain, new_text, line, context     (kind static-using | inline | namespace-using | capture)

`domain` is the domain whose commit changes the entry: the arrival domain of the type named (L1, Q1), the domain that creates the namespace (capture).
Entries are located by occurrence, never by line (the line is informative).
Refuses (exit 1) an entry that a cut would move or shift in a way the tools do not follow (see `check_cuts`).
"""
import collections
import json
import os
import re
import sys

from common import *  # noqa: F401,F403

sys.path.insert(0, TOOLS)
import r5_lib as L  # noqa: E402

sys.stdout.reconfigure(encoding='utf-8')
table = load_table()
cur, name_idx, unmatched, _ = dll_world(table)
tbl = L.R5(WORK)
tfs = {r: TestFile(r) for r in test_files() if r.endswith('.cs')}
type_by_old = {}
for t in tbl.types:
    type_by_old.setdefault(t['old_type'], t)

problems = []

# --- 1. literals with an old test class name
lit = []
for rel, tf in sorted(tfs.items()):
    for a, b, tok, rank in L.literal_occurrences(tf.src, tbl.old_test_names):
        t = type_by_old[tok]
        ctx = tf.text[tf.text.rfind('\n', 0, a) + 1:tf.text.find('\n', b)].strip()
        if 'Not used by' in ctx:
            k = 'not-used-message'
        elif re.search(re.escape(tok) + r'_', tf.text[a:a + len(tok) + 2]) or re.search(r'"' + re.escape(tok) + r'"', tf.text[a - 1:b + 1]) and 'GetTempPath' in ctx:
            k = 'temp-folder-prefix'
        elif re.search(re.escape(tok) + r':', tf.text[a:b + 2]):
            k = 'message-prefix'
        else:
            k = 'other'
        lit.append(dict(file=rel, line=tf.text.count('\n', 0, a) + 1, cls=k, old=tok, new=t['new_type'], rank=rank, domain=t['domain'],
                        text=ctx[:200], off=a))
with open(WORK + '/r5-literals.tsv', 'w', encoding='utf-8', newline='') as f:
    f.write('file\tline\tclass\told\tnew\trank\tdomain\ttext\n')
    for r in sorted(lit, key=lambda r: (r['file'], r['off'])):
        f.write('\t'.join([r['file'], str(r['line']), r['cls'], r['old'], r['new'], str(r['rank']), r['domain'], r['text'].replace('\t', ' ').replace('\n', ' ')]) + '\n')
print('literals with an old test class name:', len(lit), dict(collections.Counter(r['cls'] for r in lit)), 'in', len({r['file'] for r in lit}), 'files')
for r in lit:
    if r['cls'] == 'other':
        print('  other:', r['file'], r['line'], r['text'][:100])

# --- 2. qualified references (Q1)
qual = []
dom_of_ns = collections.defaultdict(list)
for t in tbl.types:
    dom_of_ns[t['old_namespace']].append(t['domain'])
for rel, tf in sorted(tfs.items()):
    for o in L.qualified_occurrences(tf.src, tbl):
        if o['kind'] == 'unknown':
            problems.append('%s:%d: a qualified chain the tables do not know: %s' % (rel, tf.text.count('\n', 0, o['start']) + 1, o['text']))
            continue
        if o['spaced']:
            problems.append('%s: a qualified chain with spaces inside: %s' % (rel, o['text']))
            continue
        line = tf.text.count('\n', 0, o['start']) + 1
        ctx = tf.text[tf.text.rfind('\n', 0, o['start']) + 1:tf.text.find('\n', o['end'])].strip()
        if o['kind'] == 'namespace':
            doms = dom_of_ns[o['text']]
            dom = max(doms, key=L.DOMAINS.index)
            qual.append(dict(file=rel, kind='namespace-using', text=o['text'], rank=o['rank'], domain=dom, new_text='', line=line, context=ctx, off=o['start']))
        else:
            t = tbl.type_by_old_full[o['type_full']][0]
            kind = 'static-using' if ctx.startswith('using static') else 'inline'
            qual.append(dict(file=rel, kind=kind, text=o['text'], rank=o['rank'], domain=t['domain'], new_text=t['new_namespace'], line=line, context=ctx, off=o['start']))

# --- 3. capture: partially qualified DLL names that stop binding once Alundra.Tests.<Seg> exists
seg_types = collections.defaultdict(set)
for v in cur.values():
    for ns, nm in ((v['cur_ns'], v['cur_name']), (v['final_ns'], v['final_name'])):
        parts = ns.split('.')
        for i, p in enumerate(parts):
            seg_types[p].add(nm)
            if i + 1 < len(parts):
                seg_types[p].add(parts[i + 1])
seg_domain = {}
for r in tbl.cs:
    for seg in r['namespace'].split('.')[2:]:
        d = r['domain']
        if seg not in seg_domain or L.DOMAINS.index(d) < L.DOMAINS.index(seg_domain[seg]):
            seg_domain[seg] = d
for rel, tf in sorted(tfs.items()):
    rank = collections.Counter()
    for m in L.CAP.finditer(tf.masked):
        seg, nxt = m.group(1), m.group(2)
        if seg in seg_domain and nxt in seg_types.get(seg, ()):
            text = seg + '.' + nxt
            rank[text] += 1
            ls = tf.text.rfind('\n', 0, m.start()) + 1
            qual.append(dict(file=rel, kind='capture', text=text, rank=rank[text], domain=seg_domain[seg], new_text='Alundra.' + text,
                             line=tf.text.count('\n', 0, m.start()) + 1, context=tf.text[ls:tf.text.find('\n', m.start())].strip(), off=m.start()))
with open(WORK + '/r5-qualified.tsv', 'w', encoding='utf-8', newline='') as f:
    f.write('file\tkind\ttext\trank\tdomain\tnew_text\tline\tcontext\n')
    for r in sorted(qual, key=lambda r: (r['file'], r['off'])):
        f.write('\t'.join([r['file'], r['kind'], r['text'], str(r['rank']), r['domain'], r['new_text'], str(r['line']), r['context'][:200].replace('\t', ' ')]) + '\n')
print('qualified entries:', len(qual), dict(collections.Counter(r['kind'] for r in qual)))
for r in qual:
    if r['kind'] == 'capture':
        print('  capture %s:%d %s -> domain %s' % (r['file'][len('Alundra.Tests/'):], r['line'], r['text'], r['domain']))

# --- 4. the cuts must not move or shift an entry in a way the tools do not follow
cut_domain = collections.defaultdict(list)   # source file -> [(type, domain)]
for c in tbl.cuts:
    cut_domain[c['source_file']].append((c['main_type'], c['domain']))
entries = [(r['file'], r['off'], r['domain'], r['text'], r['kind']) for r in qual] + [(r['file'], r['off'], r['domain'], r['old'], 'literal') for r in lit]
for rel, cuts in cut_domain.items():
    tf = tfs[rel]
    dcut = max((d for _, d in cuts), key=L.DOMAINS.index)
    head_end = len(L.split_head(tf.text.lstrip('﻿'))[0])
    blocks = {}
    for tname, _ in cuts:
        s, e = A.block_of(tf.text if not tf.text.startswith('﻿') else tf.text, tname)
        blocks[tname] = (s, e)
    for file, off, dom, text, kind in entries:
        if file != rel:
            continue
        if off < head_end:
            if L.DOMAINS.index(dom) > L.DOMAINS.index(dcut):
                problems.append('%s: the %s entry %r is in the header (copied by the cut at %s) and changes later (%s)' % (rel, kind, text, dcut, dom))
            continue
        inside = [n for n, (s, e) in blocks.items() if s <= off < e]
        if inside:
            if L.DOMAINS.index(dom) > L.DOMAINS.index(dcut):
                problems.append('%s: the %s entry %r is inside the cut block %s and changes later (%s > %s)' % (rel, kind, text, inside[0], dom, dcut))
            continue
        # in the rest of the file: an earlier occurrence of the same text inside a block cut before the entry changes would shift its rank
        for file2, off2, dom2, text2, kind2 in entries:
            if file2 == rel and text2 == text and kind2 == kind and off2 < off:
                if any(s <= off2 < e for s, e in blocks.values()) and L.DOMAINS.index(dom) > L.DOMAINS.index(dcut):
                    problems.append('%s: the %s entry %r has an earlier occurrence in a cut block: its rank would shift' % (rel, kind, text))
print('cut / entry checks:', 'OK' if not problems else '%d problem(s)' % len(problems))
for p in problems:
    print('  PROBLEM', p)
sys.exit(1 if problems else 0)
