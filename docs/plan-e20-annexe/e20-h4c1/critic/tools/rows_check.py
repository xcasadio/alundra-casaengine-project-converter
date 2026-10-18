"""rows_check.py (critic of h4c1): every rock of the other agent's key has the animation row table[old row 0][col]; and the recycled-slot discrimination stale versus clean."""
import json, sys
from mydis import u32
tab = [[u32(0x800237F4 + 32 * r + 4 * c) for c in range(8)] for r in range(4)]
print('table 0x800237F4 rows:', tab)
tot = disc = clean_ok = stale_ok = bad = n = 0
for nm in ('flat_east', 'flat_south', 'flat_west', 'flat_north', 'flat_east_x200', 'wall_col9', 'deepwater_cols7_9'):
    d = json.load(open(sys.argv[1] + '/earth1_%s.json' % nm))
    lastrow = {}
    live = {}
    for r in d['rows']:
        cur = r['rocks']
        for s, e in cur.items():
            if s in live and live[s]['status'] == 4:
                lastrow[s] = live[s]['adir']; del live[s]
            if s not in live:
                col = ((e['dir_t'] + 2) & 0x1c) >> 2
                n += 1
                if tab[0][col] != e['adir']:
                    bad += 1
                prev = lastrow.get(s)
                if prev is not None:
                    tot += 1
                    stale = tab[prev][col]; clean = tab[0][col]
                    if stale != clean:
                        disc += 1
                        clean_ok += e['adir'] == clean
                        stale_ok += e['adir'] == stale
                live[s] = dict(adir=e['adir'], status=e['status'])
            live[s]['status'] = e['status']; live[s]['adir'] = e['adir']
        for s in list(live):
            if s not in cur:
                lastrow[s] = live[s]['adir']; del live[s]
print('rocks examined', n, '; rocks whose row differs from table[old row 0][col]:', bad)
print('recycled-slot rocks', tot, '; stale model != clean model for', disc, '; observed = clean', clean_ok, '; observed = stale', stale_ok)
