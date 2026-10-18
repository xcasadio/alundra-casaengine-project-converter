"""turn_stats.py - h4d2 discovery: in the world keys with targets, counts the turn events of the balls (a tick where the ball's direction word changes while C63 acts) and, for each, whether the numeric gap
between the direction of the target (the REAL GetDirectionToTarget 0x8002E814 of the positions at the start of the tick) and the current direction exceeds 16 (the cases where the binary turns the long way).
usage: python -B turn_stats.py KEYDIR"""
import json, os, sys
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(HERE, '..', 'vech'))
import vtrace as V
M = 0xFFFFFFFF
m = V.TM()
def dirto(dx, dy): return m.call(0x8002E814, dx & M, dy & M) & M
tot = long = 0
for name in sys.argv[2:]:
    d = json.load(open(os.path.join(sys.argv[1], name + '.json')))
    rows = d['rows']; n = lg = 0; ex = []
    for k in range(1, len(rows)):
        a, b = rows[k - 1]['ents'], rows[k]['ents']
        for i, e in b.items():
            p = a.get(i)
            if not p or e['type'] != 23 or p['id'] != e['id'] or e['status'] != 2 or p['status'] != 2: continue
            if e['dir_t'] != p['dir_t'] and p['b278_slot'] is not None or (e['b278_slot'] is not None and p['b278_slot'] is None and e['dir_t'] != p['dir_t']):
                tslot = str(e['b278_slot'] if e['b278_slot'] is not None else p['b278_slot'])
                tp = a.get(tslot)
                if tp is None: continue
                t = dirto(tp['x'] - p['x'], tp['y'] - p['y'])
                gap = abs(t - p['dir_t'])
                n += 1
                if gap > 16:
                    lg += 1; ex.append((rows[k]['t'], p['dir_t'], t, e['dir_t']))
    print('%-30s turn events %3d, numeric gap > 16 (long way): %2d  examples (tick, cur, target, new): %s' % (name, n, lg, ex[:3]))
    tot += n; long += lg
print('TOTAL turn events', tot, 'long way', long)
