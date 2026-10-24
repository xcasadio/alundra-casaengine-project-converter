"""itemops.py : every item/money opcode (0x82 give, 0x83 count>=, 0x84 use, 0x97 spend, 0x98 add, 0x99 try spend, 0x9A enough) in all programs of all maps,
flagging the 30 chain maps. READ-ONLY."""
import sys, collections
sys.path.insert(0, '.')
import evlib as L
CHAIN = {389, 390, 476, 478, 392, 391, 416, 163, 162, 165, 164, 172, 169, 170, 171, 173, 174, 175, 10, 179, 176, 177, 178, 180, 181, 182, 135, 183, 184, 185}
OPS = {0x82: 'give', 0x83: 'count>=', 0x84: 'use', 0x97: 'spend', 0x98: 'addmoney', 0x99: 'tryspend', 0x9A: 'enough'}
rows = []
for m in L.all_map_ids():
    doc = L.load_events(m)
    if not doc: continue
    seen = set()
    for X in 'ABCDEF':
        for i, s in enumerate(doc['EventCodes%sTable' % X]):
            if (X, s) in seen: continue
            seen.add((X, s))
            for pc, op, v, bad in L.sweep(doc, s, L.prog_end(doc, s)):
                if op in OPS and v:
                    rows.append((m, m in CHAIN, X, i, pc, OPS[op], list(v[1:])))
if len(sys.argv) > 1 and sys.argv[1] == 'chain':
    for r in rows:
        if r[1]: print(r)
else:
    for r in rows: print(r)
