"""families.py - groups the sites of sites_all.json by (opcode, sprite type) and counts sites, programs and placements (records) per map. Read-only."""
import json, re, collections, os
HERE = os.path.dirname(os.path.abspath(__file__))
js = json.load(open(os.path.join(HERE, 'sites_all.json')))
fam = collections.OrderedDict()
for s in js:
    users = s['users'] or ['(none)']
    for u in users:
        m = re.match(r'rec(\d+):([MG])(\d+)\((.*)\)', u)
        key = (s['op'], ('%s%s %s' % (m.group(2), m.group(3), m.group(4))) if m else '(no placement)')
        d = fam.setdefault(key, dict(sites=set(), recs=set(), maps=set()))
        d['sites'].add((s['map'], s['slot'], s['idx'], s['pc']))
        if m:
            d['recs'].add((s['map'], int(m.group(1))))
        d['maps'].add(s['map'])
out = []
for (op, name), d in sorted(fam.items(), key=lambda kv: (kv[0][0], -len(kv[1]['recs']))):
    byslot = collections.Counter(x[1] for x in d['sites'])
    out.append('0x%02X  %-34s sites %2d (%s)  placements %3d  maps %s' % (op, name[:34], len(d['sites']), ' '.join('%s%d' % (k, v) for k, v in sorted(byslot.items())), len(d['recs']), sorted(d['maps'])))
open(os.path.join(HERE, 'families.txt'), 'w', encoding='utf-8').write('\n'.join(out) + '\n')
print('\n'.join(out))
