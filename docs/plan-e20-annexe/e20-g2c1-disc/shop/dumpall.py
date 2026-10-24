"""dumpall.py MAP : every program (all tables) with opcode names. Read-only. python -B -I -X utf8"""
import sys
sys.path.insert(0, '.')
import evlib as L
m = int(sys.argv[1])
doc = L.load_events(m)
print('map', m, 'Codes', len(doc['Codes']))
for X in 'ABCDEF':
    print(X, doc['EventCodes%sTable' % X])
seen = set()
for X in 'ABCDEF':
    for i, s in enumerate(doc['EventCodes%sTable' % X]):
        if (X, s) in seen: continue
        seen.add((X, s))
        L.show(doc, X, i)
