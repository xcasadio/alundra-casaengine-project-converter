import sys, os
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), 'h4o'))
import fire1_lib as L
def cal(res):
    seen = {}
    for r in res['rows']:
        for s, e in r['ents'].items():
            if s == 0: continue
            k = (s, e['id'])
            seen.setdefault(k, dict(first=r['t'], type=e['type'], last=r['t']))['last'] = r['t']
    inuse_free = next((r['t'] for r in res['rows'] if r['t'] > 40 and r['inuse'] == 0), None)
    return seen, inuse_free
for faithful in (True, False):
    for pf in (True, False):
        res = L.run(ticks=200, faithful=faithful, pf=pf)
        seen, free = cal(res)
        balls = sorted(v['first'] for v in seen.values() if v['type'] == 23)
        col = [v for v in seen.values() if v['type'] == 20][0]
        print('faithful=%s pf=%s: column first=%d last=%d ; %d balls first ticks %s ; last entity gone after tick %d ; object freed at %s ; draws total %d' % (
            faithful, pf, col['first'], col['last'], len(balls), balls, max(v['last'] for v in seen.values()), free, sum((r['draws'] or 0) for r in res['rows'])))
