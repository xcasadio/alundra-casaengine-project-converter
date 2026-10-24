"""itemcond.py : every `0x83 If number of item >= value` and `0x84 Use item` site (all maps, real programs only: table index 0 skipped) with the item id, to see whether anything gates on an item sold in a shop. READ-ONLY."""
import sys, collections
sys.path.insert(0, '.')
import evlib as L
SOLD = {36: 'Herbe', 37: 'Potion L', 38: 'Extrait magique', 39: 'Breuvage de soin', 41: 'Potion S', 18: 'Veste en cuir', 20: 'Veste en argent', 83: 'Recipient de vie', 51: 'Anneau d orc', 53: 'Bracelet', 55: 'Anneau regen', 7: 'Baton magique'}
rows = []
for m in L.all_map_ids():
    doc = L.load_events(m)
    if not doc: continue
    seen = set()
    for X in 'ABCDEF':
        for i, s in enumerate(doc['EventCodes%sTable' % X]):
            if i == 0 and s == 0: continue
            if (X, s) in seen: continue
            seen.add((X, s))
            for pc, op, v, bad in L.sweep(doc, s, L.prog_end(doc, s)):
                if op in (0x83, 0x84) and v:
                    rows.append((m, X, i, pc, '>=' if op == 0x83 else 'use', v[1], v[2] if op == 0x83 else None))
for r in rows:
    flag = ' <== SOLD ITEM %s' % SOLD[r[5]] if r[5] in SOLD else ''
    print(r, flag)
print(len(rows), 'sites')
