"""warps.py DEST... : every 0x53 (Change map) in any event program (all maps) whose destination map (v1|v2<<8) is in DEST. READ-ONLY."""
import sys, collections
sys.path.insert(0, '.')
import evlib as L
dest = set(int(a) for a in sys.argv[1:])
SLOTN = 'ABCDEF'
for m in L.all_map_ids():
    doc = L.load_events(m)
    if not doc: continue
    seen = set()
    for X in SLOTN:
        for i, s in enumerate(doc['EventCodes%sTable' % X]):
            if (X, s) in seen: continue
            seen.add((X, s))
            for pc, op, v, bad in L.sweep(doc, s, L.prog_end(doc, s)):
                if op == 0x53 and v and (v[1] | v[2] << 8) in dest:
                    print('map %3d %s[%d]@%d 0x53 %s' % (m, X, i, pc, list(v[1:])))
