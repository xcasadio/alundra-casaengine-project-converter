"""rocks_table.py (own): human-readable per-rock table from a keys/earth1_*.json file."""
import json, sys
d = json.load(open(sys.argv[1]))
rows = d['rows']
live = {}
allr = []
for r in rows:
    t = r['tick']
    cur = set()
    for s, e in r['rocks'].items():
        s = int(s)
        cur.add(s)
        if s in live and live[s]['track'][-1]['status'] == 4:
            del live[s]
        if s not in live:
            live[s] = dict(slot=s, birth=t, track=[])
            allr.append(live[s])
        live[s]['track'].append(dict(t=t, **e))
    for s in list(live):
        if s not in cur:
            del live[s]
print('# %s: %d rocks; columns: n birth slot dir_t row | Zspeed(ForceZ at birth) | land(first tick z==terrain+1 raw & fz==0, status 2) E3tick(status 3) animend(reset_AC=1) status4 | final x,y (dx,dy from hero) | sound46 tick' % (d['scenario'], len(allr)))
hx, hy = d['meta']['hero']
snd = {}
for r in rows:
    for j in r['sounds']:
        snd.setdefault(r['tick'], []).append(j)
for n, b in enumerate(allr):
    tr = b['track']
    land = next((r['t'] for r in tr if r['pz'] == 1 and r['fz'] == 0 and r['status'] == 2), None)
    st3 = next((r['t'] for r in tr if r['status'] == 3), None)
    ae = next((r['t'] for r in tr if r['reset_AC'] == 1), None)
    st4 = next((r['t'] for r in tr if r['status'] == 4), None)
    fzb = tr[0]['fz']
    print('%2d %3d slot%2d dir=%2d row=%s | birth fz after tick %d (%#x) | land=%s E3=%s animend=%s st4=%s | (%d,%d) d=(%d,%d) | snd46@%s' % (
        n, b['birth'], b['slot'], tr[0]['dir_t'], tr[0]['adir'], b['birth'], fzb & 0xFFFFFFFF, land, st3, ae, st4, tr[-1]['px'] >> 16, tr[-1]['py'] >> 16, (tr[-1]['px'] >> 16) - hx, (tr[-1]['py'] >> 16) - hy,
        st3 if any('46' in j for j in snd.get(st3, [])) else None))
