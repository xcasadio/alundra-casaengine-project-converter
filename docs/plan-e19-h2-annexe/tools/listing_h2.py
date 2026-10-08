"""listing_h2.py - python -B listing_h2.py <map> <first pc> <last pc> : list the exported event program (size table of the live repo)."""
import sys, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
sys.stdout.reconfigure(encoding="utf-8")
import m
mid = int(sys.argv[1]); a = int(sys.argv[2]); b = int(sys.argv[3])
mp = m.Map(mid)
C = mp.C
boundary = sum(2 * len(t) for t in mp.T.values())
labels = {}
for k, t in mp.T.items():
    for i, off in enumerate(t):
        if off >= boundary:
            labels.setdefault(off, []).append("%s[%d]" % (k, i))
pc = boundary
print("map", mid, "boundary", boundary, "len", len(C))
while pc < len(C) and pc <= b:
    line, sz = m.fmt(C, pc)
    if pc >= a:
        if pc in labels:
            print("  ;; entry", " ".join(labels[pc]))
        print(line)
    pc += sz
