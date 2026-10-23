"""dump_map.py <map> - lists the placements of a map with the sprite name, header programs, zone, and the event program indices.
Run: python -B -I dump_map.py 27"""
import json, sys, csv, os
R = r"D:/development/repo/alundra-casaengine-project-converter"
HERE = os.path.dirname(os.path.abspath(__file__))
rows = list(csv.DictReader(open(R + '/docs/plan-e14-annexe/e14-roster/roster.tsv', encoding='utf-8'), delimiter='\t'))
names = {}
namesM = {}
for r in rows:
    if r['page'] == 'G':
        names[int(r['table_index'])] = r['name_fr']
    if r['page'] == 'M':
        namesM[int(r['table_index'])] = r['name_fr']
mp = int(sys.argv[1])
d = json.load(open(R + '/data-extracted/data/map_%d.json' % mp, encoding='utf-8-sig'))
si = d['SpriteInfo']; recs = si['SpriteRecords']
print('map', mp, 'Info.MapId', d['Info']['MapId'], 'Map size', d['Map']['Width'], d['Map']['Height'])
for k, e in enumerate(si['Entities']['Entities']):
    if e is None: continue
    i = e['SpriteTableIndex']
    r = recs[i] if i < len(recs) else None
    h = r['Header'] if r else {}
    print('rec %3d en=%d sprite %3d %-34s C%-3s D%-3s E%-3s F%-3s A%-3s  pos(%3d,%3d,h%2d) zone[%d..%d]x[%d..%d] dir=%d idx(A%d B%d C%d D%d E%d F%d) kind=%s MF=0x%X' % (
        k, e['IsEnabled'], i, (namesM.get(i, '?') if r is not None else names.get(i, '?'))[:34], h.get('ProgramTick'), h.get('ProgramTouch'), h.get('ProgramDeactivate'), h.get('ProgramInteract'), h.get('ProgramLoad'),
        e['XPos'], e['YPos'], e['Height'], e['XMin'], e['XMax'], e['YMin'], e['YMax'], e['SpriteDirection'],
        e['EventCodesA_LoadIndex'], e['EventCodesB_MapIndex'], e['EventCodesC_TickIndex'], e['EventCodesD_TouchIndex'], e['EventCodesE_DeactivateIndex'], e['EventCodesF_InteractIndex'],
        ((h['CanPickup'] >> 1) & 3) if h else '', h.get('MoreFlags', 0)))
