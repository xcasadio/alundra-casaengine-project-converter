"""cmp_v6.py - E20.e0a4 discovery (scratch only): the record-born rows of out/rec_tick.json (sprite 3, loop 2) against the interface-born V6 rows of
repo/docs/plan-e20-annexe/e20-e0a/oracle/out/v6.json (HEAD copy, read only), pass by pass for ticks S and S+1: status, trigger, +0x1C, +0xA8, update/draw lists, counter, A runs."""
import os, json
HERE = os.path.dirname(os.path.abspath(__file__))
V6 = os.path.join(HERE, 'repo', 'docs', 'plan-e20-annexe', 'e20-e0a', 'oracle', 'out', 'v6.json')
v6 = {}
for line in open(V6):
    r = json.loads(line)
    v6[r['id']] = r
rec = {r['label']: r for r in json.load(open(os.path.join(HERE, 'out', 'rec_tick.json')))['rows']}
PASSES = ('events', 'counters', 'lists', 'anim', 'physics')
out = []
for mine, theirs in (('REC-H1-SPR3', 'BT-H1'), ('REC-H2-SPR3', 'BT-H2')):
    r, b = rec[mine], v6[theirs]
    S = r['S']
    diffs = []
    for i in (0, 1):
        t = next(x for x in r['ticks'] if x['k'] == i)
        a_mine = sum(1 for x in r['a_turns'] if x[0] <= S + i)
        for nm in PASSES:
            p, q = t['passes'][nm], b['ticks'][i][nm]
            mv = (p['status'], p['trig'], p['c1c'], p['cnt_A8'], p['in_update'], p['in_draw'], p['count'])
            tv = (q['status'], q['trig'], q['c1c'], q['delay'], q['in_update'], q['in_draw'], q['count'])
            if mv != tv:
                diffs.append((i, nm, mv, tv))
        a_theirs = b['ticks'][i]['physics']['a_runs']
        out.append('%s vs %s  tick S+%d: A turns so far (record) %d, A runs so far (V6) %d' % (mine, theirs, i, a_mine, a_theirs))
    out.append('%s vs %s: %s' % (mine, theirs, 'IDENTICAL on (status, trig, +0x1C, +0xA8, update, draw, counter) for the five passes of S and S+1' if not diffs else 'DIFFER: %s' % diffs))
open(os.path.join(HERE, 'out', 'cmp_v6.txt'), 'w', newline='\n').write('\n'.join(out) + '\n')
print('\n'.join(out))
