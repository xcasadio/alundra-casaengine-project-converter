import csv, json, collections
CORE8 = {389, 390, 476, 478, 392, 391, 416, 163}
CHAIN30 = {389, 390, 476, 478, 392, 391, 416, 163, 162, 165, 164, 172, 169, 170, 171, 173, 174, 175, 10, 179, 176, 177, 178, 180, 181, 182, 135, 183, 184, 185}
COMBAT7 = {14, 15, 44, 115, 116, 117, 362}
rows = list(csv.DictReader(open('census-sites.tsv', encoding='utf-8'), delimiter='\t'))
agg = {}
def get(op):
    return agg.setdefault(op, {'site_programs': 0, 'unique_map_pc': set(), 'maps': set(), 'slots': collections.Counter(),
                               'core8_sp': 0, 'chain30_sp': 0, 'combat7_sp': 0,
                               'core8_pc': set(), 'chain30_pc': set(), 'combat7_pc': set(), 'core8_maps': set(), 'chain30_maps': set(), 'combat7_maps': set(),
                               'entity_slots': collections.Counter()})
for r in rows:
    op = int(r['opcode'], 16); m = int(r['map']); pc = int(r['pc'])
    a = get(op)
    a['site_programs'] += 1
    a['unique_map_pc'].add((m, pc))
    a['maps'].add(m)
    a['slots'][r['slot']] += 1
    if m in CORE8:
        a['core8_sp'] += 1; a['core8_pc'].add((m, pc)); a['core8_maps'].add(m)
    if m in CHAIN30:
        a['chain30_sp'] += 1; a['chain30_pc'].add((m, pc)); a['chain30_maps'].add(m)
    if m in COMBAT7:
        a['combat7_sp'] += 1; a['combat7_pc'].add((m, pc)); a['combat7_maps'].add(m)
out = {}
for op, a in sorted(agg.items()):
    out['%02X' % op] = {
        'site_programs': a['site_programs'],
        'unique_sites': len(a['unique_map_pc']),
        'maps': len(a['maps']),
        'slots': dict(a['slots']),
        'core8_sp': a['core8_sp'], 'core8_sites': len(a['core8_pc']), 'core8_maps': sorted(a['core8_maps']),
        'chain30_sp': a['chain30_sp'], 'chain30_sites': len(a['chain30_pc']), 'chain30_maps': len(a['chain30_maps']),
        'combat7_sp': a['combat7_sp'], 'combat7_sites': len(a['combat7_pc']),
    }
json.dump(out, open('corpus_by_opcode.json', 'w'), indent=1)
print(len(out), 'distinct opcodes reached')
tot = sum(a['site_programs'] for a in agg.values())
print('total site-programs', tot, 'unique (map,pc)', sum(len(a['unique_map_pc']) for a in agg.values()))
