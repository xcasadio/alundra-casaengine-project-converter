"""Merges skipped-sites.tsv (the sm1 rows) with red_values.tsv into corpus/sm1-sites.tsv: for each of the 187 sites its shape, its logic entity, what the DLL gives today
(the Result writers that reach it), and the real-bytes scenario / arc that covers it.
Usage: python -B sites_table.py OUT.tsv"""
import csv
import sys
from collections import Counter

REPO = "D:/development/repo/alundra-casaengine-project-converter"
HERE = __file__.rsplit('/', 1)[0].rsplit(chr(92), 1)[0]
sites = [r for r in csv.DictReader(open(REPO + '/docs/plan-e19-sm-annexe/skipped-sites.tsv', encoding='utf-8'), delimiter='\t') if r['opcode'].upper() in ('60', '61', '3C', '3D')]
red = {}
for r in csv.DictReader(open(HERE + '/red_values.tsv', encoding='utf-8'), delimiter='\t'):
    red[(r['map'], r['prog'], r['pc'])] = r


def shape(m, slot, prog, op):
    if m == 27 and prog == 2 or m == 400 and slot == 'C':
        return ('plate-phase-loop', 'owner (the plate)', 'RB-02 / AR-27, AR-400')
    if m == 27 and prog == 36:
        return ('stack-chain-with-0x43', 'the 0x43 target (record 35, then each stacked object)', 'RB-03')
    if m in (27, 29, 30) or (m == 113 and prog == 9):
        return ('plate-wait', 'owner (the plate)', 'RB-01 / RB-04 / RB-06 (+ RB-05 shape for 30)')
    if m == 113:
        return ('plate-toggle', 'owner (the plate)', 'RB-07')
    if m == 136:
        return ('hero-switch', 'owner (the switch); group 0x81 = the hero', 'RB-08')
    if m == 159 and slot == 'B':
        return ('map-event-hero', 'the hero (owner = logic = hero, program B)', 'RB-09')
    if m == 159:
        return ('bubble-restart', 'owner (the bubble)', 'RB-10')
    if m == 226:
        return ('zone-wait-B', 'none: 0x3D ignores the logic entity (owner = logic = hero after 0x42)', 'RB-11')
    if m == 398:
        return ('map-event-0x43-target', 'the 0x43 target (the item), owner = hero', 'RB-12')
    if m in (77, 78):
        return ('wood-rides-hero', 'owner (the wood); group 0x81 = the hero', 'RB-13 / RB-14')
    return ('?', '?', '?')


rows = []
for r in sites:
    m = int(r['map'])
    key = (r['map'], '%s[%s]' % (r['slot'], r['program']), r['pc'])
    rv = red[key]
    sh, logic, cover = shape(m, r['slot'], int(r['program']), r['opcode'])
    rows.append([r['map'], r['map_name'], '%s[%s]' % (r['slot'], r['program']), r['pc'], r['opcode'].upper(), r['operands'], sh, logic, rv['reaching_result_writers_today'], rv['first_reader_after'], cover])
with open(sys.argv[1], 'w', encoding='utf-8', newline='\n') as f:
    f.write('map\tworld\tprogram\tpc\topcode\toperands\tshape\tlogic_entity\tresult_writers_reaching_the_site_today\tfirst_reader_after\tcovered_by\n')
    for x in rows:
        f.write('\t'.join(x) + '\n')
c = Counter((x[6]) for x in rows)
print(len(rows), dict(c))
c2 = Counter((x[8].split(',')[0].split(':')[0] if x[8] else '') for x in rows)
print(dict(c2))
