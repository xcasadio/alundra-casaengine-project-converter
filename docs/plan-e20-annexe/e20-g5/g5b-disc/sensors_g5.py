"""sensors_g5.py - in the maps that hold a pushable, every program site of the sensor opcodes 0x07 (entity in area), 0x3E (player riding), 0x60/0x61 (riding), 0x2C (no entity by function), 0x3B (player in area)
with the placements that run the program.  Linear sweep limited to each program's own range.  Read-only."""
import os, sys, json, csv, collections
sys.path.insert(0, 'D:/development/repo/alundra-casaengine-project-converter/docs/plan-e20-annexe/e20-g3/dll')
import decode_events as de
R = 'D:/development/repo/alundra-casaengine-project-converter'
HERE = os.path.dirname(os.path.abspath(__file__))
rows = list(csv.DictReader(open(R + '/docs/plan-e14-annexe/e14-roster/roster.tsv', encoding='utf-8'), delimiter='\t'))
G = {int(r['table_index']): r['name_fr'] for r in rows if r['page'] == 'G'}
M = {int(r['table_index']): r['name_fr'] for r in rows if r['page'] == 'M'}
cen = json.load(open(os.path.join(HERE, 'census_g5.json')))
maps = sorted({r['map'] for r in cen['rows']})
WATCH = {0x07: 'entity-in-area', 0x3E: 'player-riding', 0x60: 'riding', 0x61: 'riding2', 0x2C: 'no-entity-by-fn', 0x3B: 'player-in-area'}
SLOTK = dict(A='EventCodesA_LoadIndex', C='EventCodesC_TickIndex', D='EventCodesD_TouchIndex', E='EventCodesE_DeactivateIndex', F='EventCodesF_InteractIndex', B='EventCodesB_MapIndex')
out = []
for mp in maps:
    d = json.load(open(R + '/data-extracted/data/map_%d.json' % mp, encoding='utf-8-sig'))
    ents = d['SpriteInfo']['Entities']['Entities']
    doc = de.load_map(mp)
    users = collections.defaultdict(list)
    for k, e in enumerate(ents):
        if e is None or not e['IsEnabled']: continue
        page = 'M' if e['SpriteDirection'] & 0x80 else 'G'
        nm = (M if page == 'M' else G).get(e['SpriteTableIndex'], '?')
        for s, key in SLOTK.items():
            if e[key]:
                users[(s, e[key] & 0x7f)].append('rec%d:%s' % (k, nm[:18]))
    out.append('=== map %d' % mp)
    for slot, idx, st, en in de.all_programs(doc):
        if slot == 'B':
            pass
        for pc, op, v, bad in de.sweep(doc, st, en):
            if bad: break
            if op in WATCH:
                u = users.get((slot, idx), [])
                out.append('   %s[%d]@%d %s %s  users: %s' % (slot, idx, pc, WATCH[op], list(v[1:]), ', '.join(u[:4]) + (' (+%d)' % (len(u) - 4) if len(u) > 4 else '')))
txt = '\n'.join(out)
open(os.path.join(HERE, 'sensors_g5.txt'), 'w', encoding='utf-8').write(txt + '\n')
print(len(out))
