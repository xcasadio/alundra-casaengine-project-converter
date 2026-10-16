import sys
import corpus
m, s, p = sys.argv[1], sys.argv[2], int(sys.argv[3])
mid = int(m)
base, d = corpus.load(mid)
codes = d['Codes']
entry = d[corpus.TABLE_KEYS[s]][p]
lim = int(sys.argv[4]) if len(sys.argv) > 4 else 60
print(base, s, p, 'entry', entry)
for pc, op, ops in corpus.decode(codes, entry, count=lim, stop_at_end=True):
    print('%5d  %02X %-34s %s' % (pc, op, corpus.NAMES.get(op, '?'), ops))
