#!/usr/bin/env python
# -*- coding: utf-8 -*-
"""Markdown tables for report.md, computed from opcodes.tsv and skipped-sites.tsv (label script2). Read-only."""
import csv, collections, json, os
OUT = os.path.dirname(os.path.abspath(__file__))
rows = list(csv.DictReader(open(OUT + '/opcodes.tsv', encoding='utf-8'), delimiter='\t'))
by = {r['opcode']: r for r in rows}
sites = list(csv.DictReader(open(OUT + '/skipped-sites.tsv', encoding='utf-8'), delimiter='\t'))
cons = collections.Counter(); tot = collections.Counter()
for s in sites:
    tot['0x' + s['opcode']] += 1
    if s['next_is_Result_consumer'] == 'Y':
        cons['0x' + s['opcode']] += 1
total_sp = sum(int(r['corpus_site_programs']) for r in rows)
out = []
def p(x=''):
    out.append(x)

# 1 states
p('### Tableau des etats')
p()
p('| Etat DLL | Opcodes (sur 256) | Site-programmes du corpus | Part |')
p('|---|---|---|---|')
st = collections.OrderedDict()
for r in rows:
    s = st.setdefault(r['dll_state'], [0, 0])
    s[0] += 1; s[1] += int(r['corpus_site_programs'])
for k in ('FAITHFUL', 'PARTIAL', 'NOOP_DELIBERATE', 'LOOP_BUILTIN', 'SKIPPED_BY_SIZE', 'TERMINATES_SIZE0', 'NOT_IN_TABLE'):
    v = st[k]
    p('| %s | %d | %d | %.2f %% |' % (k, v[0], v[1], 100.0 * v[1] / total_sp))
p('| **Total** | **256** | **%d** | 100 %% |' % total_sp)
p()

# 2 unported ranking
p('### Opcodes sautes par leur taille (29), classes par usage dans le corpus')
p()
p('| Opcode | Libelle de la table | Sites | Cartes | Creneaux | Chaine 30 | Coeur 8 | Combat 7 | Suivi d un consommateur de Result | Cartes principales |')
p('|---|---|---|---|---|---|---|---|---|---|')
sk = [r for r in rows if r['dll_state'] == 'SKIPPED_BY_SIZE']
sk.sort(key=lambda r: -int(r['corpus_site_programs']))
mapsby = collections.defaultdict(collections.Counter)
for s in sites:
    mapsby['0x' + s['opcode']][(s['map'], s['map_name'])] += 1
for r in sk:
    op = r['opcode']
    mp = '; '.join('%s x%d' % (m[0], c) for m, c in mapsby[op].most_common(4))
    c = '%d/%d' % (cons[op], tot[op]) if tot[op] else '-'
    p('| %s | %s | %s | %s | %s | %s | %s | %s | %s | %s |' % (op, r['name_in_size_table'] or '(vide)', r['corpus_site_programs'], r['corpus_maps'], r['corpus_slots'] or '-',
                                                    r['chain30_sites'], r['story_core8_sites'], r['combat7_sites'], c, mp or '-'))
p()

# 3 top usage
p('### 30 opcodes les plus frequents du corpus (tous etats)')
p()
p('| Rang | Opcode | Libelle | Etat | Sites | Cartes | Chaine 30 |')
p('|---|---|---|---|---|---|---|')
top = sorted(rows, key=lambda r: -int(r['corpus_site_programs']))[:30]
for i, r in enumerate(top, 1):
    p('| %d | %s | %s | %s | %s | %s | %s |' % (i, r['opcode'], r['name_in_size_table'][:38], r['dll_state'], r['corpus_site_programs'], r['corpus_maps'], r['chain30_sites']))
p()

# 4 citations
cand = [r for r in rows if r['dll_state'] in ('FAITHFUL', 'PARTIAL', 'NOOP_DELIBERATE') and r['binary_cited_in_dll_comment'] == 'N']
cand.sort(key=lambda r: -int(r['corpus_site_programs']))
p('### Gestionnaires portes dont le commentaire de la DLL ne cite ni adresse ni « binaire » (%d, %d site-programmes, %.1f %% du corpus)' % (len(cand), sum(int(r['corpus_site_programs']) for r in cand), 100.0 * sum(int(r['corpus_site_programs']) for r in cand) / total_sp))
p()
p('`' + '`, `'.join('%s (%s)' % (r['opcode'], r['corpus_site_programs']) for r in cand[:40]) + '` ...')
p()
txt = '\n'.join(out)
parts = txt.split('### ')
for n, part in zip(['states', 'skipped', 'top', 'uncited'], parts[1:]):
    open(OUT + '/frag-' + n + '.txt', 'w', encoding='utf-8', newline='\n').write(part.split('\n', 2)[2].strip() + '\n')
open(OUT + '/tables.txt', 'w', encoding='utf-8', newline='\n').write(txt)
print(txt)
