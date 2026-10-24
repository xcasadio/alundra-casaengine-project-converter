"""dumpmap.py MAP : placements (global vs map page, names) with program indices, map events. READ-ONLY."""
import sys, json, csv
sys.path.insert(0, '.')
import evlib as L
R = L.ROOT
rows = list(csv.DictReader(open(R + '/docs/plan-e14-annexe/e14-roster/roster.tsv', encoding='utf-8'), delimiter='\t'))
nameG = {int(r['table_index']): r['name_fr'] for r in rows if r['page'] == 'G'}
nameM = {int(r['table_index']): r['name_fr'] for r in rows if r['page'] == 'M'}
m = int(sys.argv[1])
d = L.load_map(m)
si = d['SpriteInfo']; recs = si['SpriteRecords']
print('map', m, 'size', d['Map']['Width'], d['Map']['Height'])
for k, e in enumerate(si['Entities']['Entities']):
    if e is None: continue
    i = e['SpriteTableIndex']
    glob_ = (e['SpriteDirection'] & 0x80) == 0
    nm = nameG.get(i, '?') if glob_ else (recs[i]['Name'] if (i < len(recs) and recs[i] and 'Name' in recs[i]) else 'M%d' % i)
    px = e['XPos']*12+12; py = e['YPos']*8+8; pz = e['Height']*8
    print('rec %3d en=%d %s%-3d %-30s pos(%3d,%3d,h%2d)=px(%4d,%4d,z%3d) dir=%3d zone[%d..%d]x[%d..%d] A%d B%d C%d D%d E%d F%d' % (
        k, e['IsEnabled'], 'G' if glob_ else 'M', i, nm[:30], e['XPos'], e['YPos'], e['Height'], px, py, pz, e['SpriteDirection'], e['XMin'], e['XMax'], e['YMin'], e['YMax'],
        e['EventCodesA_LoadIndex'], e['EventCodesB_MapIndex'], e['EventCodesC_TickIndex'], e['EventCodesD_TouchIndex'], e['EventCodesE_DeactivateIndex'], e['EventCodesF_InteractIndex']))
print('MapEvents:')
for j, r in enumerate(si['MapEvents']['Records']):
    if r: print('  ', j, r)
