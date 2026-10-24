"""scan_shop.py - every placement (global or map page) whose A/C/D/E/F program executes 0x41 [C,4] (SpriteProgramIndexes[2]=4)
or [x, 4] swaps, with the sprite and the program indices. READ-ONLY. python -B -I -X utf8 scan_shop.py"""
import sys, json, collections
sys.path.insert(0, '.')
import evlib as L
SL = (('A','EventCodesA_LoadIndex'),('B','EventCodesB_MapIndex'),('C','EventCodesC_TickIndex'),('D','EventCodesD_TouchIndex'),('E','EventCodesE_DeactivateIndex'),('F','EventCodesF_InteractIndex'))
res = collections.defaultdict(list)
for m in L.all_map_ids():
    doc = L.load_events(m)
    if not doc: continue
    d = L.load_map(m); si = d.get('SpriteInfo')
    if not si: continue
    for k, e in enumerate(si['Entities']['Entities']):
        if e is None or not e['IsEnabled']: continue
        glob_ = (e['SpriteDirection'] & 0x80) == 0
        for X, key in SL:
            idx = e[key] & 0x7f
            if idx == 0: continue
            tab = doc['EventCodes%sTable' % X]
            if idx >= len(tab): continue
            s = tab[idx]; end = L.prog_end(doc, s)
            for pc, op, v, bad in L.sweep(doc, s, end):
                if op == 0x41 and v and v[1] == 2 and v[2] == 4:
                    res[m].append((k, 'G' if glob_ else 'M', e['SpriteTableIndex'], X, idx, pc, e['XPos'], e['YPos'], e['Height']))
tot = 0
for m, lst in sorted(res.items()):
    print('map', m, len(lst))
    for r in lst:
        print('   rec %3d %s%-3d  slot %s idx %d @%d  pos(%d,%d,h%d)' % r)
    tot += len(lst)
print('total', tot)
