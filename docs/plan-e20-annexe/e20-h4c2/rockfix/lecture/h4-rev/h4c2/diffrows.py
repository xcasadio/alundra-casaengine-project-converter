import json, sys
a = json.load(open(sys.argv[1])); b = json.load(open(sys.argv[2]))
ra = {r['id']: r for r in a['rows']}; rb = {r['id']: r for r in b['rows']}
print('rows', len(ra), len(rb), 'kicks base', [t for t, _ in a['kicks']], 'fix', [t for t, _ in b['kicks']])
for i in sorted(set(ra) | set(rb)):
    x, y = ra.get(i), rb.get(i)
    if x is None or y is None:
        print('id', i, 'only in', 'base' if y is None else 'fix'); continue
    d = [k for k in x if k != 'track' and x[k] != y[k]]
    if d: print('id', i, {k: (x[k], y[k]) for k in d})
print('sounds base', len(a['sounds']), 'fix', len(b['sounds']), 'only45 base', sorted(t for t, i in a['sounds'] if i == 45) , 'fix', sorted(t for t, i in b['sounds'] if i == 45))
