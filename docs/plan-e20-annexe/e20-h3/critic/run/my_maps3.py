import json, sys
from my_maps import SIZES, DATA, programs, decode
mid = int(sys.argv[1]); tab = sys.argv[2]; pi = int(sys.argv[3])
d = json.load(open(DATA + 'map_%d.json' % mid, encoding='utf-8-sig'))
codes, tabs, offs = programs(d)
off = tabs[tab][pi]
nxt = [o for o in offs if o > off]; end = nxt[0] if nxt else len(codes)
print('map', mid, tab, pi, 'bytes', off, end)
for (p, op, raw, name) in decode(codes, off, end):
    print('  %4d %02x %-45s %s' % (p, op, name, list(raw) if raw else ''))
