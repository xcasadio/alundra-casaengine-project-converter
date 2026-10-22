import json, collections
SM4 = 'C:/Users/casad/AppData/Local/Temp/claude/D--development-repo-alundra-casaengine-project-converter/b00d1a72-420d-4acc-ab34-0c25fbdad3a1/scratchpad/sm-prep/sm4'
cat = json.load(open(SM4 + '/tests-catalog.json'))
rm = json.load(open(SM4 + '/removal-matrix.json'))
print(len(cat), collections.Counter(t['kind'] for t in cat))
print('red today by kind:', collections.Counter(t['kind'] for t in cat if t['redToday']))
print('green today by kind:', collections.Counter(t['kind'] for t in cat if not t['redToday']))
print('needs vocabulary:', sorted({n for t in cat for n in t['needs']}))
for key, val in rm.items():
    print(key, json.dumps(val)[:600])
# recompute
calc = collections.defaultdict(list)
for t in cat:
    if t['redToday']:
        for n in t['needs']:
            calc[n].append(t['id'])
for k, v in calc.items():
    print('RECOMPUTED', k, len(v), v)
# the per-test columns
for t in cat:
    print(t['id'], t['kind'], 'red' if t['redToday'] else 'green', t['needs'], t.get('redCount'), '/', t.get('count'), t['name'][:70])
