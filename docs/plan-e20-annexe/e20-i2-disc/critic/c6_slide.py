import json, os, collections
ROOT = r'D:/development/repo/alundra-casaengine-project-converter/data-extracted/data'
vals = collections.defaultdict(list); keys=None
for n in range(483):
    p = os.path.join(ROOT, 'map_%d.json' % n)
    if not os.path.exists(p): continue
    m = json.load(open(p, encoding='utf-8-sig'))
    i = m['Info']
    if keys is None: keys = list(i.keys()); print(keys)
    vals[i.get('SlideEffectId')].append(n)
for k, v in vals.items(): print(k, len(v), v[:10] if len(v) > 10 else v)
