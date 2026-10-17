import json, os, sys, collections
out = sys.argv[1]
for sp in ('beannoide', 'muruta'):
    v = json.load(open(os.path.join(out, 'vectors-%s.json' % sp)))
    e1 = [x for x in v if x.get('handler') == 'E1']
    nz = [x for x in e1 if x['entity_before'].get('contents_3C', 0) != 0]
    drop = []
    contents_drop = collections.Counter()
    contents_nodrop = collections.Counter()
    types = collections.Counter()
    pattern_ok = 0
    excl = 0
    kinds = collections.Counter()
    for x in e1:
        j = x['journal']
        for k in j:
            kinds[k[0]] += 1
        excl += sum(1 for k in j if k[0] != 'effect_3BE74')
        sp_i = [i for i, k in enumerate(j) if k[0] == 'spawn_39F58']
        c = x['entity_before'].get('contents_3C', 0)
        if sp_i:
            drop.append(x)
            contents_drop[c] += 1
            a = j[sp_i[0]][1]
            types[(c, a[2])] += 1
            if sp_i[0] + 1 < len(j) and j[sp_i[0] + 1][0] == 'sound' and j[sp_i[0] + 1][1] == [84] and a[0] == 0 and a[1] == 0 and a[6] == 0 and a[7] == 0:
                pattern_ok += 1
        else:
            contents_nodrop[c] += 1
    print(sp, 'E1', len(e1), 'contents!=0', len(nz), 'with spawn', len(drop), 'pattern [0,0,..,0,0]+sound84', pattern_ok, 'excluded', excl)
    print('  kinds', dict(kinds))
    print('  drop contents', dict(contents_drop))
    print('  nodrop contents', dict(contents_nodrop))
    print('  (contents,type)', dict(types))
# scenarios
for f in sorted(os.listdir(out)):
    if f.endswith('.json') and not f.startswith('vectors-'):
        d = json.load(open(os.path.join(out, f)))
        calls = d.get('calls') if isinstance(d, dict) else d
        if calls is None:
            continue
        e1 = [c for c in calls if c.get('handler') == 'E1']
        if e1:
            nd = sum(1 for c in e1 if any(k[0] == 'spawn_39F58' for k in c.get('journal', [])))
            print(f, 'E1 calls', len(e1), 'with spawn', nd)
