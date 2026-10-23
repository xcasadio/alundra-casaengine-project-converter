"""series.py FILE REC - the (frame, x, y, anim) series of one watched record in a probe output, run-length compressed (consecutive frames with the same velocity are one line). Read-only."""
import re, sys
f, rid = sys.argv[1], sys.argv[2]
rows = []
for l in open(f, encoding='utf-8'):
    m = re.match(r'^\s*(\d+) (\S+)\s*\| H (\S+) (\S+) (\S+) a(\d+) d(\d+) f(\S+) ctl(\S+) xc=(\S+) adj(\d) \|(.*)$', l)
    if not m:
        continue
    r = re.search(r'\[rec%s (\S+) (\S+) (\S+) a(\d+) d(\d+) s(\d+) f(\S+) adj(\d) xc=(\S+) g(\S+)\]' % rid, m.group(12))
    if r:
        rows.append((int(m.group(1)), float(r.group(1)), float(r.group(2)), int(r.group(4)), float(m.group(3)), float(m.group(4)), m.group(10), m.group(2)))
out = []
prev = None
start = None
for i, (fr, x, y, a, hx, hy, xc, hold) in enumerate(rows):
    vel = None if prev is None else (round(x - prev[1], 3), round(y - prev[2], 3), a)
    if prev is not None and vel == (out[-1][3] if out else None) and out:
        out[-1][1] = fr
        out[-1][4] = (x, y)
    else:
        out.append([fr, fr, (prev[1], prev[2]) if prev else (x, y), vel, (x, y), a])
    prev = (fr, x, y, a)
for o in out:
    print('frames %d-%d  v=%s  from %s to %s  anim %s' % (o[0], o[1], o[3], o[2], o[4], o[5]))
