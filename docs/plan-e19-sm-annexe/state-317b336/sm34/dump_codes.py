import json, os, sys
os.chdir('D:/development/repo/alundra-casaengine-project-converter')
idx = json.load(open('alundra-project/Maps/world-index.json', encoding='utf-8-sig'))
BS = chr(92)

def load(m):
    w = idx[str(m)].replace(BS, '/')
    folder = os.path.dirname(w)
    name = os.path.basename(w)[:-6]
    return json.load(open(f'alundra-project/{folder}/events/{name}.events.json', encoding='utf-8-sig'))

m = int(sys.argv[1]); a = int(sys.argv[2]); b = int(sys.argv[3])
d = load(m)
c = d['Codes']
print('map', m, 'len', len(c))
print(' '.join(f'{i}:{c[i]:02X}' for i in range(a, b)))
for k in ('A', 'B', 'C', 'D', 'E', 'F'):
    print(k, d['EventCodes%sTable' % k])
