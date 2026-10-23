"""pillar_refs.py - in the pillar maps, the program sites that name a pillar record by its raw id in the search-type operand v1 (< 0x80) of the opcodes that take one; and the 0x80|0 (self) searches
whose logic entity could be a pillar (a program run BY a pillar). Read-only."""
import sys, json, os, collections
sys.path.insert(0, 'D:/development/repo/alundra-casaengine-project-converter/docs/plan-e20-annexe/e20-g3/dll')
import decode_events as de
HERE = os.path.dirname(os.path.abspath(__file__))
cen = json.load(open(os.path.join(HERE, 'census_g5.json')))
SEARCH_OPS = {0x07, 0x42, 0x43, 0x2E, 0x62, 0x63, 0x64, 0x65, 0x5E, 0x60, 0x61, 0x67, 0xAC, 0x87, 0x8D, 0xA9, 0xAA, 0x2C, 0x2D}
for fam in ('PILIER', 'STATUE', 'BLOC', 'POUSSOIR'):
    maps = sorted({r['map'] for r in cen['rows'] if r['fam'] == fam})
    print('==', fam)
    for mp in maps:
        recs = {r['rec'] for r in cen['rows'] if r['fam'] == fam and r['map'] == mp}
        doc = de.load_map(mp)
        hits = []
        for slot, idx, st, en in de.all_programs(doc):
            for pc, op, v, bad in de.sweep(doc, st, en):
                if bad: break
                if op in SEARCH_OPS and len(v) > 1 and v[1] in recs:
                    hits.append('%s[%d]@%d op0x%02X %s' % (slot, idx, pc, op, list(v[1:])))
        print('  map %d recs %s : %s' % (mp, sorted(recs), hits if hits else '-'))
