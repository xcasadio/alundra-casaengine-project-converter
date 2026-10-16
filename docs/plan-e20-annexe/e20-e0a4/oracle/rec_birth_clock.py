"""rec_birth_clock.py - E20.e0a4 discovery (scratch only): the REC-* rows of out/rec_tick.json in the shape of e20-e0a5 out/birth_clock.json (rows[], ticks[] by k = tick - S, the
same keys plus call_no = number of animation-pass calls 0x80038E4C that reached the entity up to that tick), and a cross-check of the harness against the committed rows
LOAD27 and WAVE of repo/docs/plan-e20-annexe/e20-e0a5/oracle/out/birth_clock.json (HEAD copy, read only).
Writes out/rec_birth_clock.json, out/rec_birth_clock.txt."""
import os, json
HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.join(HERE, 'out')
rec = json.load(open(os.path.join(OUT, 'rec_tick.json')))['rows']
bc = {r['label']: r for r in json.load(open(os.path.join(HERE, 'repo', 'docs', 'plan-e20-annexe', 'e20-e0a5', 'oracle', 'out', 'birth_clock.json')))['rows']}
rows = []
lines = ['# rec_birth_clock: k = tick - S (S = creation tick; 0 for the map load and the hero); first k of end_AC = 1 and of status 4; cnt1c and call_no at k = 0..3; loops_B0 first k per value; box changes (k where hbx-px .. bh differ from k-1)']
for r in rec:
    ticks = []
    for x in r['clock']:
        ticks.append({q: x[q] for q in ('k', 'tick', 'status', 'cnt1c', 'call_no', 'end_AC', 'loops_B0', 'frame', 'cnt_A8', 'anim_c', 'dir_c', 'adir', 'px', 'py', 'pz', 'hbx', 'hby', 'hbz', 'bw', 'bd', 'bh')})
    rows.append(dict(label=r['label'], scenario=r['scenario'], op=r['op'], sprite=r['sprite'], birth_pass=r['birth_pass'], new_slot=r['new_slot'], S=r['S'], switch=r['switches'],
                     stale_trigger=(r['stale_before_creation'] or {}).get('trig228'), a_turn_ticks=[t[0] - r['S'] for t in r['a_turns']], source='rec_tick.py', ticks=ticks))
    t = ticks
    fe = next((x['k'] for x in t if x['end_AC'] == 1), None)
    f4 = next((x['k'] for x in t if x['status'] == 4), None)
    fl = {}
    for x in t:
        fl.setdefault(x['loops_B0'], x['k'])
    chg = [b['k'] for a, b in zip(t, t[1:]) if any(a[q] - (a['px'] if q == 'hbx' else a['py'] if q == 'hby' else a['pz'] if q == 'hbz' else 0) != b[q] - (b['px'] if q == 'hbx' else b['py'] if q == 'hby' else b['pz'] if q == 'hbz' else 0) for q in ('hbx', 'hby', 'hbz', 'bw', 'bd', 'bh'))]
    c03 = [(x['cnt1c'], x['call_no']) for x in t if x['k'] is not None and 0 <= x['k'] <= 3]
    lines.append('%-13s S=%-3d sprite %-3d birth %-13s slot %-2s stale trig %-3s A turns at k=%s | end_AC first k=%s status4 k=%s | (cnt1c, call_no) k=0..3 %s | loops_B0 %s | box changes k=%s' % (
        r['label'], r['S'], r['sprite'], r['birth_pass'], r['new_slot'], rows[-1]['stale_trigger'], rows[-1]['a_turn_ticks'][:3], fe, f4, c03, fl, chg[:8]))
json.dump(dict(scenario='e0a4 record births (birth_clock shape)', rows=rows), open(os.path.join(OUT, 'rec_birth_clock.json'), 'w', newline='\n'), indent=None, sort_keys=True, separators=(',', ':'))
# cross-checks against the committed oracle (same machine family): LOAD27 = REC-LOAD, WAVE (MovePlayer birth) vs REC-ME (map-event birth) relative to S
R = {r['label']: r for r in rows}
def proj(t, keys):
    return [tuple(x[q] for q in keys) for x in t]
keys = ('k', 'status', 'cnt1c', 'end_AC', 'loops_B0', 'cnt_A8')
a = [x for x in bc['LOAD27']['ticks'] if 1 <= x['k'] <= 33]
b = [x for x in R['REC-LOAD']['ticks'] if 1 <= x['k'] <= 33]
lines.append('check REC-LOAD vs birth_clock LOAD27 on %s, k = 1..33: %s' % (keys, 'IDENTICAL' if proj(a, keys) == proj(b, keys) else 'DIFFER'))
w = {x['k']: x for x in bc['WAVE']['ticks']}
me = {x['k']: x for x in R['REC-ME']['ticks']}
same = all((w[k]['status'], w[k]['cnt1c'], w[k]['end_AC'], w[k]['loops_B0'], w[k]['cnt_A8'], w[k]['hbx'] - w[k]['px'], w[k]['hby'] - w[k]['py'], w[k]['hbz'] - w[k]['pz'], w[k]['bw'], w[k]['bd'], w[k]['bh']) ==
           (me[k]['status'], me[k]['cnt1c'], me[k]['end_AC'], me[k]['loops_B0'], me[k]['cnt_A8'], me[k]['hbx'] - me[k]['px'], me[k]['hby'] - me[k]['py'], me[k]['hbz'] - me[k]['pz'], me[k]['bw'], me[k]['bd'], me[k]['bh'])
           for k in range(0, 28) if k in w and k in me)
lines.append('check REC-ME (record, map-event pass) vs birth_clock WAVE (SpawnWarpEntity in MovePlayer) on status, cnt1c, end_AC, loops_B0, cnt_A8, box relative to the position, k = 0..27: %s' % ('IDENTICAL' if same else 'DIFFER'))
for lab_rec, lab_bc in (('REC-SW1', 'SW1'), ('REC-SW2', 'SW2'), ('REC-SW5', 'SW5'), ('REC-SW1-2', 'SW1-2'), ('REC-SWD1', 'SWD1'), ('REC-SWD2', 'SWD2'), ('REC-SW0', 'SW0'), ('REC-H1', 'WAVE')):
    f1 = next((x['k'] for x in R[lab_rec]['ticks'] if x['end_AC'] == 1), None)
    f2 = next((x['k'] for x in bc[lab_bc]['ticks'] if x['end_AC'] == 1), None)
    lines.append('compare first end_AC: %-10s (record, loop 2) k=%s   %-6s (birth_clock, MovePlayer) k=%s   %s' % (lab_rec, f1, lab_bc, f2, 'same' if f1 == f2 else 'DIFFERENT'))
open(os.path.join(OUT, 'rec_birth_clock.txt'), 'w', newline='\n').write('\n'.join(lines) + '\n')
print('\n'.join(lines))
