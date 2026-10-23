"""summ.py DIR NAME... - compact view of the probe outputs: per watched record the frames where its position, animation, direction or the hero's contact change, plus the skipped-opcode table. Read-only."""
import re, sys, os
d = sys.argv[1]
names = sys.argv[2:] or sorted(f[:-4] for f in os.listdir(d) if f.endswith('.txt') and not f.endswith('.trace.txt'))
frame_re = re.compile(r'^\s*(\d+) (\S+)\s*\| H (\S+) (\S+) (\S+) a(\d+) d(\d+) f(\S+) ctl(\S+) xc=(\S+) adj(\d) \|(.*)$')
rec_re = re.compile(r'\[rec(\d+) (?:absent|(\S+) (\S+) (\S+) a(\d+) d(\d+) s(\d+) f(\S+) adj(\d) xc=(\S+) g(\S+))\]')
for n in names:
    lines = open(os.path.join(d, n + '.txt'), encoding='utf-8').read().split('\n')
    print('=== ' + n)
    for l in lines:
        if l.startswith('case') or l.startswith('EXCEPTION'):
            print(l)
    prev_h = None
    prev = {}
    first = {}
    for l in lines:
        m = frame_re.match(l)
        if not m:
            continue
        fr, hold, hx, hy, hz, ha, hd, hf, ctl, hxc, hadj, rest = m.groups()
        hs = (hx, hy, ha, hxc)
        if hs != prev_h:
            print('  f%-3s %-3s hero (%s,%s) a%s d%s xc=%s f=%s' % (fr, hold, hx, hy, ha, hd, hxc, hf))
            prev_h = hs
        for r in rec_re.finditer(rest):
            rid = r.group(1)
            if r.group(2) is None:
                continue
            x, y, z, a, dd, st, f, adj, xc, g = r.groups()[1:]
            key = (x, y, a, dd, xc)
            if prev.get(rid) != key:
                print('  f%-3s       rec%s (%s,%s) a%s d%s f=%s xc=%s' % (fr, rid, x, y, a, dd, f, xc))
                prev[rid] = key
    tail = False
    for l in lines:
        if l.startswith('skipped'):
            tail = True
        if tail:
            print(l)
