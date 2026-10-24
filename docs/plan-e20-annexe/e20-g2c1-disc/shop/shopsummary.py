"""shopsummary.py : per shop map, per B program of the shop, the item record watched, the zone, the dialog ids, the price and the item given. READ-ONLY."""
import sys
sys.path.insert(0, '.')
import evlib as L
def run(m, progs):
    doc = L.load_events(m)
    d = L.load_map(m)
    ents = d['SpriteInfo']['Entities']['Entities']
    print('== map', m)
    for X, i in progs:
        s = doc['EventCodes%sTable' % X][i]
        ins = L.sweep(doc, s, L.prog_end(doc, s))
        info = {'area': [], 'price': [], 'give': [], 'dlg': [], 'spawn': [], 'pos': [], 'other': []}
        for pc, op, v, bad in ins:
            if v is None: continue
            if op == 0x07: info['area'].append((v[1], tuple(v[2:8])))
            elif op in (0x9A, 0x99): info['price'].append((op, v[1] | v[2] << 8))
            elif op == 0x82: info['give'].append(v[1])
            elif op == 0x5C: info['dlg'].append((v[1], v[2]))
            elif op == 0x8A: info['spawn'].append((v[1], v[2] | v[3] << 8, v[4] | v[5] << 8, v[6] | v[7] << 8))
            elif op == 0x64: info['pos'].append((v[1], v[2] | v[3] << 8, v[4] | v[5] << 8, v[6] | v[7] << 8))
        print(' %s[%d] area=%s' % (X, i, info['area']))
        print('      price=%s give=%s dialogs(ent,id)=%s' % (sorted(set(info['price'])), info['give'], info['dlg']))
        print('      setpos=%s spawn=%s' % (info['pos'], info['spawn']))
run(163, [('B',2),('B',3),('B',4)])
run(258, [('B',2),('B',3),('B',4),('B',5),('B',7)])
run(298, [('B',2),('B',3),('B',4),('B',5)])
run(398, [('B',1),('B',2),('B',3),('B',4),('B',5),('B',6),('B',7)])
run(134, [('B',1),('B',2),('B',3),('B',4)])
