"""Own enumeration of the sites of 0x72 0x88 0x96 0x97 0xB3 (+0x15 0x66 0x7A) in the exported corpus, by a linear sweep of each program
(entry -> next greater entry or end).  Compared with the sm0 list docs/plan-e19-sm-annexe/skipped-sites.tsv."""
import csv, collections, sys
import cx_corpus as C

OPS = (0x72, 0x88, 0x96, 0x97, 0xB3, 0x15, 0x66, 0x7A)
mine = {}
allprog = {}
for mid in sorted(int(k) for k in C.IDX):
    try:
        base, d, folder = C.load(mid)
    except Exception as e:
        print('no events for', mid, e)
        continue
    codes = d['Codes']
    progs = C.programs(d)
    entries = sorted({e for (_, _, e) in progs})
    allprog[mid] = (base, d, folder)
    # per entry: sweep to the next greater entry
    for (s, i, e) in progs:
        nxt = [x for x in entries if x > e]
        end = nxt[0] if nxt else len(codes)
        seq = C.sweep(codes, e, end)
        for (pc, op, ops) in seq:
            if op in OPS:
                mine.setdefault((mid, pc), []).append((s, i, op, ops))

sm0 = {}
for row in csv.DictReader(open(C.REPO + '/docs/plan-e19-sm-annexe/skipped-sites.tsv', encoding='utf-8'), delimiter='\t'):
    op = int(row['opcode'], 16)
    if op in OPS:
        sm0[(int(row['map']), int(row['pc']))] = (row['slot'], int(row['program']), op, row['operands'])
print('sm0 sites for the 8 opcodes:', len(sm0), collections.Counter(v[2] for v in sm0.values()))
print('mine (with duplicates collapsed per (map,pc)):', len(mine), collections.Counter(v[0][2] for v in mine.values()))
print('maps with a site (sm0, the 5 opcodes):', len({k[0] for k, v in sm0.items() if v[2] in OPS[:5]}))
only_sm0 = sorted(set(sm0) - set(mine))
only_mine = sorted(set(mine) - set(sm0))
print('only in sm0:', only_sm0[:20], len(only_sm0))
print('only in mine:', only_mine[:40], len(only_mine))
for k in only_mine[:40]:
    print(k, mine[k][:2])
# check operands
bad = 0
for k, v in sm0.items():
    ops = mine.get(k)
    if not ops:
        continue
    m_ops = ' '.join('%02X' % b for b in ops[0][3])
    if m_ops != v[3].strip():
        bad += 1
        print('operand mismatch', k, m_ops, '|', v[3])
print('operand mismatches', bad)

print("---- classification of the extras: is there an FF (end) earlier in the same linear sweep from the program entry?")
cls = collections.Counter()
for k in only_mine:
    mid, pc = k
    base, d, folder = allprog[mid]
    codes = d['Codes']
    progs = C.programs(d)
    entries = sorted({e for (_, _, e) in progs})
    s, i, op, ops = mine[k][0]
    e = [x for (ss, ii, x) in progs if ss == s and ii == i][0]
    nxt = [x for x in entries if x > e]
    end = nxt[0] if nxt else len(codes)
    seq = C.sweep(codes, e, end)
    ff_before = any(o == 0xFF for (p, o, _) in seq if p < pc)
    cls[(op, ff_before)] += 1
    if not ff_before:
        print("NO FF BEFORE:", k, mine[k][:2], "entry", e)
print(cls)
