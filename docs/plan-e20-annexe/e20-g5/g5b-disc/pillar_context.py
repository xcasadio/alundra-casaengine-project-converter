"""pillar_context.py - for each map with a pushable (pillar/statue/bloc/poussoir): entities of the puzzle kind (switches, plates, gates, doors, chests, other pushables) with the
programs that use the plate predicates 0x60/0x61 (riding), 0x5E/0x5F, and the list of opcodes each map's programs use that the DLL does not implement.  Read-only.
Run: python -B -X utf8 pillar_context.py"""
import os, sys, json, csv, re, collections
sys.path.insert(0, 'D:/development/repo/alundra-casaengine-project-converter/docs/plan-e20-annexe/e20-g3/dll')
import decode_events as de
R = 'D:/development/repo/alundra-casaengine-project-converter'
HERE = os.path.dirname(os.path.abspath(__file__))
rows = list(csv.DictReader(open(R + '/docs/plan-e14-annexe/e14-roster/roster.tsv', encoding='utf-8'), delimiter='\t'))
G = {int(r['table_index']): r['name_fr'] for r in rows if r['page'] == 'G'}
M = {int(r['table_index']): r['name_fr'] for r in rows if r['page'] == 'M'}
cen = json.load(open(os.path.join(HERE, 'census_g5.json')))
maps = sorted({r['map'] for r in cen['rows']})
src = open(R + '/Alundra/Events/EventProgramRunner.cs', encoding='utf-8-sig').read()
cases = set()
for m_ in re.finditer(r'case\s+0x([0-9A-Fa-f]{2})\s*:', src):
    cases.add(int(m_.group(1), 16))
KIND = re.compile(r'Interrupteur|Plaque|Grille|Porte|Pilier|Statue|poussable|Bloc à|Trappe|Plateforme|Escalier|Levier|Ballon|Coffre|Torche|Flamme', re.I)
out = []
for mp in maps:
    d = json.load(open(R + '/data-extracted/data/map_%d.json' % mp, encoding='utf-8-sig'))
    ents = d['SpriteInfo']['Entities']['Entities']
    recs = d['SpriteInfo']['SpriteRecords']
    doc = de.load_map(mp)
    out.append('=== map %d (%s) size %dx%d' % (mp, d['Info'].get('MapId'), d['Map']['Width'], d['Map']['Height']))
    interesting = []
    for k, e in enumerate(ents):
        if e is None or not e['IsEnabled']: continue
        page = 'M' if e['SpriteDirection'] & 0x80 else 'G'
        i = e['SpriteTableIndex']
        nm = (M if page == 'M' else G).get(i, '?')
        if KIND.search(nm):
            sc = ''.join(s for s, key in zip('ACDEF', ('EventCodesA_LoadIndex', 'EventCodesC_TickIndex', 'EventCodesD_TouchIndex', 'EventCodesE_DeactivateIndex', 'EventCodesF_InteractIndex')) if e[key])
            interesting.append((k, page, i, nm, e['XPos'], e['YPos'], e['Height'], sc, e['SpriteDirection']))
    for it in interesting:
        out.append('   rec %3d %s%3d %-36s pos(%3d,%3d,h%2d) scripted=%s dir=0x%02X' % it)
    # opcode use per program, flagging 0x60/0x61 and the opcodes the DLL has no case for
    if doc:
        usage = collections.defaultdict(list)
        for slot, idx, st, en in de.all_programs(doc):
            for pc, op, v, bad in de.sweep(doc, st, en):
                if bad: break
                if op in (0x60, 0x61, 0x5E, 0x5F):
                    usage[op].append('%s[%d]@%d %s' % (slot, idx, pc, list(v[1:])))
                if op not in cases:
                    usage['missing 0x%02X' % op].append('%s[%d]@%d' % (slot, idx, pc))
        for k_, v_ in usage.items():
            out.append('   opcode %s: %d sites: %s' % (k_ if isinstance(k_, str) else '0x%02X' % k_, len(v_), v_[:6]))
txt = '\n'.join(out)
open(os.path.join(HERE, 'pillar_context.txt'), 'w', encoding='utf-8').write(txt + '\n')
print(txt)
