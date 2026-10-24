import sys, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import evlib as L
doc = L.load_events(72)
print(list(doc.keys())[:20])
for X in 'ABCDEF':
    print(X, len(doc['EventCodes%sTable' % X]), doc['EventCodes%sTable' % X][:5], '...', doc['EventCodes%sTable' % X][135:142])
print(len(doc['Codes']))
