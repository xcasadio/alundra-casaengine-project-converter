"""turn_stats_mine.py (critic): count the turn events of the balls in the BINARY-mode keys and how many go the long way (numeric gap > 16), with GetDirectionToTarget 0x8002E814 run on MY interpreter.
Also checks every observed turn against my own reading of 0x8007AD6C-0x8007ADB8 (numeric compare, step 4)."""
import json, sys, os, glob
import lib, myemu
from myemu import M
em = myemu.Emu()
def dirto(dx, dy):
    return em.call(0x8002E814, dx & M, dy & M)
def turn_bin(cur, t):
    if cur == t: return None
    if t < cur: return t if cur - t < 4 else cur - 4
    return t if t - cur < 4 else cur + 4
K = sys.argv[1]
tot = lg = 0
for name in ('fire1_tgt_one_pf', 'fire1_tgt_three_pf', 'fire1_tgt_two_wrong_order_pf', 'fire1_tgt_vanish75_pf', 'fire1_col_hit_pf', 'fire1_tgt_topslot_pf'):
    d = json.load(open(os.path.join(K, name + '.json')))
    rows = d['rows']
    n = l = bad = 0
    for k in range(1, len(rows)):
        a, b = rows[k - 1]['ents'], rows[k]['ents']
        for i, e in b.items():
            p = a.get(i)
            if not p or e['type'] != 23 or p['id'] != e['id'] or p['status'] != 2 or e['status'] != 2:
                continue
            # C63 acts at this tick iff b274 was 0 at the end of the previous tick (p['b274'] == 0)
            if p['b274'] != 0:
                continue
            tslot = e['b278_slot'] if e['b278_slot'] is not None else p['b278_slot']
            if tslot is None: continue
            tp = a.get(str(tslot))
            if tp is None: continue
            t = dirto((tp['x'] - p['x']), (tp['y'] - p['y']))
            exp = turn_bin(p['dir_t'], t)
            changed = e['dir_t'] != p['dir_t']
            if exp is None:
                if changed: bad += 1
                continue
            n += 1
            if abs(t - p['dir_t']) > 16: l += 1
            if e['dir_t'] != exp: bad += 1
    print('%-34s turns %3d  long-way %2d  turns not equal to my reading of the binary: %d' % (name, n, l, bad))
    tot += n; lg += l
print('TOTAL turns', tot, 'long way', lg, '=> %.1f%%' % (100.0 * lg / max(tot, 1)))
