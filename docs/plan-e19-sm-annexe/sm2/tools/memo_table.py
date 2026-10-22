"""One row per read (0x6B/6C/6D) of the corpus: flow state, linear state, calls between the last save and the read, callee opcodes."""
import collections, os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import corpus2 as C, cfg2, memo_static as M

progs, sites = M.programs_from_tsv()
rows = []
for (m, s, p) in progs:
    base, d = C.load(m)
    codes = d['Codes']; entry = d[C.TABLE_KEYS[s]][p]
    df, seen, marks, ru = cfg2.dataflow(codes, entry, M.CLOB_BIN, M.READERS)
    for (mm, ss, pp, pc, op) in sites:
        if (mm, ss, pp) != (m, s, p) or op not in M.READERS:
            continue
        axis = M.READERS[op]
        st = ','.join(sorted(df[pc][axis]))
        kind, w, over, calls = M.linear_state(codes, entry, pc, M.CLOB_BIN[axis], None)
        callee_ops = []
        for (cp, co, cops) in calls:
            if co == 0x78:
                tgt = cp + C.s16(codes, cp + 1)
                ops = []
                q = tgt
                while q < len(codes):
                    o, sz, _ = C.decode_one(codes, q)
                    ops.append('%02X' % o)
                    q += sz
                    if o in (0x7D, 0xFF):
                        break
                callee_ops.append('@%d->@%d:%s' % (cp, tgt, '/'.join(ops)))
        rows.append((m, s, p, pc, '%02X' % op, axis, st, kind, w if w is not None else '', len(calls), ';'.join(callee_ops)))
print('map\tslot\tprogram\tpc\top\taxis\tflow_state\tlinear\tlast_6A_pc\tcalls_between\tcallees')
for r in rows:
    print('\t'.join(str(x) for x in r))
