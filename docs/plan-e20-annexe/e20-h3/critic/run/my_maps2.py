import json, sys
from my_maps import SIZES, DATA, programs, decode
mid = int(sys.argv[1]); idxs = [int(x) for x in sys.argv[2:]]
d = json.load(open(DATA + 'map_%d.json' % mid, encoding='utf-8-sig'))
codes, tabs, offs = programs(d)
ents = d['SpriteInfo']['Entities']['Entities']
print('map', mid, 'tables', {k: v for k, v in tabs.items()})
for i in idxs:
    e = ents[i]
    print('record', i, e)
    for nm, key in (('A', 'EventCodesA_LoadIndex'), ('B', 'EventCodesB_MapIndex'), ('C', 'EventCodesC_TickIndex'), ('D', 'EventCodesD_TouchIndex'), ('E', 'EventCodesE_DeactivateIndex'), ('F', 'EventCodesF_InteractIndex')):
        v = e[key]
        if v & 0x80:
            pi = v & 0x7F
            off = tabs[nm][pi]
            nxt = [o for o in offs if o > off]
            end = nxt[0] if nxt else len(codes)
            print('  program %s[%d] @%d..%d' % (nm, pi, off, end))
            for (p, op, raw, name) in decode(codes, off, end):
                print('    %4d %02x %-40s %s' % (p, op, name, list(raw) if raw else ''))
        elif v:
            print('  native %s code %d' % (nm, v))
