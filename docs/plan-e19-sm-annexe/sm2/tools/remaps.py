import sys, os, collections
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import corpus2 as C, cfg2
pairs = collections.defaultdict(set)
for mid in C.map_ids():
    base, d = C.load(mid)
    codes = d['Codes']
    seen_all = {}
    for s in 'ABCDEF':
        for i, entry in enumerate(d[C.TABLE_KEYS[s]]):
            if i == 0 or entry == 0: continue
            seen, *_ = cfg2.reach_walkprogram(codes, entry)
            for pc, op in seen.items(): seen_all.setdefault(pc, (op, s, i))
    for pc, (op, s, i) in seen_all.items():
        if op == 0x38:
            idx = codes[pc+1] | (codes[pc+2] << 8); val = codes[pc+3] | (codes[pc+4] << 8)
            pairs[idx].add((val, mid))
targets = {55,56,57,58,59,60,305,306,307}
print('remap pairs (index -> value): %d distinct indexes' % len(pairs))
for idx in sorted(pairs):
    vals = sorted({v for v, m in pairs[idx]})
    print('  table[%d] <- %s   (maps %s)' % (idx, vals, sorted({m for v, m in pairs[idx]})))
hit = [(idx, v) for idx in pairs for v, m in pairs[idx] if v in targets or idx in targets]
print('remaps touching the Torla / unused Meia cluster:', hit)
