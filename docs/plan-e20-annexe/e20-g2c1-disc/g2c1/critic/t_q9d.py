import sys, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import evlib as L
m = int(sys.argv[1]); targets = set(int(x) for x in sys.argv[2:])
doc = L.load_events(m)
seen = set()
for X in 'ABCDEF':
    for i, s in enumerate(doc['EventCodes%sTable' % X]):
        if (X, s) in seen or s == 0 and i != 0: continue
        seen.add((X, s))
        for pc, op, v, bad in L.sweep(doc, s, L.prog_end(doc, s)):
            if v and len(v) > 1 and any(b in targets for b in v[1:]) and op in (0x43, 0x5D, 0x59, 0x2E, 0x2D, 0x8A, 0x8B, 0x65, 0x64, 0x62, 0x63):
                print('%s[%d]@%d 0x%02X %s %s' % (X, i, pc, op, L.SIZES.get(op, (0, '?'))[1], list(v[1:])))
