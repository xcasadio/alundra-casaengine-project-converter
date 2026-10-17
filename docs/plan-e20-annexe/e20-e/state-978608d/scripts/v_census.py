import json, glob, os, re, sys
root = r"D:\development\repo\alundra-casaengine-project-converter\alundra-project\Maps"
res = {}
for f in glob.glob(os.path.join(root, '*', '*', 'tilemap', '*.tileMap')):
    d = json.load(open(f, encoding='utf-8'))
    cp = d.get('custom_properties') or {}
    if 'AlundraCells' not in cp:
        continue
    c = json.loads(cp['AlundraCells'])
    w = c['walkability']
    n = sum(1 for v in w if v & 2)
    name = os.path.basename(os.path.dirname(os.path.dirname(f)))
    m = re.search(r'-(\d+)$', name)
    res[int(m.group(1)) if m else name] = (name, n, cp.get('BalanceLevel'))
tot = sum(v[1] for v in res.values())
print('maps', len(res), 'total cuttable', tot, 'maps with cuttable', sum(1 for v in res.values() if v[1]))
print('BalanceLevel values', sorted(set(str(v[2]) for v in res.values())))
for k in [int(a) for a in sys.argv[1:]]:
    print(k, res.get(k))
