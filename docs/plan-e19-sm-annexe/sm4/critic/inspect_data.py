import json, os, sys
PROJ = 'D:/development/repo/alundra-casaengine-project-converter/alundra-project'
idx = json.load(open(PROJ + '/Maps/world-index.json', encoding='utf-8'))
print(len(idx), list(idx.items())[:3])
mid = sys.argv[1] if len(sys.argv) > 1 else '452'
rel = idx[mid].replace(chr(92), '/')
print(rel)
base = os.path.splitext(os.path.basename(rel))[0]
p = PROJ + '/' + os.path.dirname(rel) + '/events/' + base + '.events.json'
d = json.load(open(p, encoding='utf-8'))
print(list(d.keys()))
for k, v in d.items():
    if k != 'Codes':
        print(k, str(v)[:200])
print(len(d['Codes']))
print(os.listdir(PROJ + '/' + os.path.dirname(rel)))
tm = [f for f in os.listdir(PROJ + '/' + os.path.dirname(rel) + '/tilemap') if f.endswith('.tileMap')]
print(tm)
t = json.load(open(PROJ + '/' + os.path.dirname(rel) + '/tilemap/' + tm[0], encoding='utf-8'))
print(list(t.keys()))
for layer in t['object_layers']:
    print(layer['name'], len(layer['objects']))
    if layer['objects']:
        print(json.dumps(layer['objects'][0])[:700])
