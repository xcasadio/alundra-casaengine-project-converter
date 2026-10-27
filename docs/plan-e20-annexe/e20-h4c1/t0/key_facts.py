"""key_facts.py (h4c1 T0): reads the filed keys of the earth tome 1 (disc/keys/*.json, remade with the slot index words) and prints the facts the probes P1 to P9 of the DLL
must reproduce: the life of each rock (birth, landing, status 3, the end of the animation, status 4), the position at the tick of E3, the animation row, the box, the slope, the
sounds. Read-only; usage: python -B key_facts.py KEYDIR [name ...]"""
import json
import os
import sys

KEYDIR = sys.argv[1]
NAMES = sys.argv[2:] or ['earth1_flat_east', 'earth1_flat_south', 'earth1_flat_west', 'earth1_flat_north', 'earth1_flat_east_x200', 'earth1_deepwater_cols7_9']
W42 = 42 * 65536 - 1


def lives(rows):
    """rocks as lists of (tick, slot, record), one list per life (a life starts at cnt1c == 1)."""
    cur = {}
    done = []
    for r in rows:
        t = r['tick']
        seen = set()
        for s, e in r['rocks'].items():
            seen.add(s)
            if e['cnt1c'] == 1 and s in cur:
                done.append(cur.pop(s))
            cur.setdefault(s, []).append((t, int(s), e))
        for s in list(cur):
            if s not in seen:
                done.append(cur.pop(s))
    done.extend(cur.values())
    return sorted(done, key=lambda l: (l[0][0], l[0][1]))


for name in NAMES:
    d = json.load(open(os.path.join(KEYDIR, name + '.json')))
    rows = d['rows']
    snd = {}
    for r in rows:
        for s in r['sounds']:
            snd.setdefault(r['tick'], []).append(s)
    print('==', name, 'ticks', len(rows), 'hero dir', d['meta']['direction'], 'hero', d['meta']['hero'])
    print('   lives:', len(lives(rows)))
    for lf in lives(rows):
        t0, slot = lf[0][0], lf[0][1]
        rec = {t: e for t, _, e in lf}
        t3 = next((t for t, _, e in lf if e['status'] == 3), None)
        t4 = next((t for t, _, e in lf if e['status'] == 4), None)
        ac = next((t for t, _, e in lf if e['reset_AC'] == 1), None)
        last = lf[-1][0]
        landed = None
        if t3:
            prev, cur = rec.get(t3 - 1), rec[t3]
            noxy = prev is not None and prev['px'] == cur['px'] and prev['py'] == cur['py']
            e3xy = (cur['px'] - prev['px'], cur['py'] - prev['py']) if prev else None
        else:
            noxy, e3xy = None, None
        box = [(t - t3, e['bw'] >> 16, e['bd'] >> 16, e['bh'] >> 16) for t, _, e in lf if t3 and e['bw'] != W42]
        slopes = sorted({e['slope_18C'] for _, _, e in lf})
        print('   slot %2d birth %3d dir %2d adir %d | st3 %s (L=%s) AC %s st4 %s last %s | xy-step-at-st3 %s | slopes %s | non-42 box ticks(after st3) %s' % (
            slot, t0, lf[0][2]['dir_t'], lf[0][2]['adir'], t3, (t3 - 1) if t3 else None, ac, t4, last, e3xy, slopes, [b[0] for b in box][:20]))
        if box:
            print('        box values', sorted({b[1:] for b in box}))
    print('   sounds:', {t: v for t, v in sorted(snd.items())})
