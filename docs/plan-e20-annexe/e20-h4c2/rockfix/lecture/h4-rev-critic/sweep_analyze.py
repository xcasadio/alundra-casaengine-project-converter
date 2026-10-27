"""Analysis of the interruption sweep (cancel_fix.py / cancel_pf.py for T = 150..200).
For each T: type-17 entities at status 2 at the end of tick T-1 (the rocks the cancel can reach), the sound 45 count at tick T, the
rocks still alive and their last tick seen; the same for the unmodified binary.  Usage: python -I sweep_analyze.py <sweepdir>"""
import json
import os
import re
import sys

d = sys.argv[1]
print('T   | FIX: air(st2@T-1) snd45@T  alive-after  lastseen(id11)  | BASE: air(st2@T-1) snd45@T lastseen(id11)')
for T in range(150, 201):
    out = []
    for kind in ('fix', 'base'):
        fj = os.path.join(d, '%s_%d.json' % (kind, T))
        ft = os.path.join(d, '%s_%d.txt' % (kind, T))
        if not os.path.exists(fj):
            out.append(None)
            continue
        rows = json.load(open(fj))
        txt = open(ft).read()
        r0 = rows[0]
        assert r0['t'] == T - 1, (r0['t'], T)
        air = [e for e in r0['ents'] if e['type'] == 17 and e['st'] == 2]
        m = re.search(r'sound journal around the cancel: (\[.*?\])\n', txt)
        snd = eval(m.group(1)) if m else []
        n45 = sum(1 for (t, k, v) in snd if t == T and v == 45)
        m2 = re.search(r'last tick seen per rock id: (\{.*\})', txt)
        last = eval(m2.group(1)) if m2 else {}
        out.append((len(air), n45, last.get(11), sorted(e['id'] for e in air)))
    f, b = out
    print('%d | %s | %s' % (T, f and (f[0], f[1], f[2], f[3]), b and (b[0], b[1], b[2], b[3])))
