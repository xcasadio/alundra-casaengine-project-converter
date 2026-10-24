"""giveids.py ID... : every `0x82 Give item` site (all maps) giving one of the ids, programs deduplicated by (map, slot, start). READ-ONLY."""
import sys, collections
sys.path.insert(0, '.')
import evlib as L
want = set(int(a) for a in sys.argv[1:])
rows = collections.defaultdict(list)
for m in L.all_map_ids():
    doc = L.load_events(m)
    if not doc: continue
    seen = set()
    for X in 'ABCDEF':
        for i, s in enumerate(doc['EventCodes%sTable' % X]):
            if i == 0 and s == 0: continue   # table[0] sweeps the map header bytes, not a program
            if (X, s) in seen: continue
            seen.add((X, s))
            for pc, op, v, bad in L.sweep(doc, s, L.prog_end(doc, s)):
                if op == 0x82 and v and v[1] in want:
                    rows[v[1]].append((m, X, i, pc))
for k in sorted(rows):
    print('item', k, ':', rows[k])
