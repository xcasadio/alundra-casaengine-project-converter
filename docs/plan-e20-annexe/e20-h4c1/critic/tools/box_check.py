"""box_check.py (critic of h4c1): ticks (relative to the first status-3 tick, E3) at which the box of a rock differs from its default 42x28x11, per animation row, in the other agent's earth1_flat_east key."""
import collections
import json
import sys
d = json.load(open(sys.argv[1] + '/earth1_flat_east.json'))
live = {}
res = []
for r in d['rows']:
    t = r['tick']
    for s, e in r['rocks'].items():
        if s in live and live[s]['rows'][-1][1]['status'] == 4:
            del live[s]
        if s not in live:
            live[s] = dict(birth=t, rows=[])
            res.append(live[s])
        live[s]['rows'].append((t, e))
    for s in list(live):
        if s not in r['rocks']:
            del live[s]
sizes = collections.Counter()
for b in res:
    e3 = next((t for t, e in b['rows'] if e['status'] == 3), None)
    first = b['rows'][0][1]
    base = (first['bw'], first['bd'], first['bh'])
    small = [t - e3 for t, e in b['rows'] if (e['bw'], e['bd'], e['bh']) != base]
    last = b['rows'][-1]
    sizes[(first['adir'], tuple(small))] += 1
    b['last_status_tick'] = last[0]
print('(animation row at birth, ticks after E3 where the box is not the default) -> number of rocks:', dict(sizes))
print('default box', [(first['bw'] + 1) / 65536, (first['bd'] + 1) / 65536, (first['bh'] + 1) / 65536])
