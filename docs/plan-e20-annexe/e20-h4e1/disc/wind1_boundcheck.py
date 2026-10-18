"""wind1_boundcheck.py - h4e1 discovery: proves that the '_dec' keys (decoy: the highest occupied slot is never a candidate) are the same under the binary query and under 'inclusive bound + binary sort':
every row of the key (everything the key records) must be identical.  usage: python -B wind1_boundcheck.py KEYDIR"""
import json, os, sys
import wind1_key as K
D = sys.argv[1]
ok = True
for name, kw in K.scen_list():
    if '_dec_' not in name or 'gateoff' in name:
        continue
    kw = dict(kw)
    kw['query_fix'] = 'bound'
    res = K.run(name, **kw)
    ref = json.load(open(os.path.join(D, name + '.json')))
    a = json.loads(json.dumps(res['rows'], sort_keys=True))
    same = a == ref['rows']
    ok &= same
    print('%-36s inclusive-bound rows identical to the binary rows: %s' % (name, same))
print('ALL IDENTICAL' if ok else 'DIFFERENCES')
