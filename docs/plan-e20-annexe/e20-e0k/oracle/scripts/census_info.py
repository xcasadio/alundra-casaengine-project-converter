import sys, os, json, glob, collections
D = r"D:/development/repo/alundra-casaengine-project-converter/data-extracted/data"
cnt = collections.Counter(); gv = collections.Counter(); ex = {}
for f in sorted(glob.glob(D + '/map_*.json')):
    n = os.path.basename(f)
    if n == 'map_alundra.json': continue
    try:
        d = json.load(open(f, encoding='utf-8-sig'))
    except Exception as e:
        continue
    inf = d.get('Info')
    if not inf: continue
    cnt[inf.get('SlideEffectId')] += 1
    gv[(inf.get('Gravity'), inf.get('ZViscosity'))] += 1
    ex.setdefault(inf.get('SlideEffectId'), []).append(n)
print('SlideEffectId (header byte +8):', dict(cnt))
print('(Gravity, ZViscosity):', dict(gv))
for k, v in ex.items():
    print(k, v[:12])
