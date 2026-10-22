"""Row-level mutants (whole handler: Result and memo outputs) of the 4 opcodes, evaluated over the frozen vectors. Complements
mutants_vectors.py (formula-only). The owner decoy of the vectors is Pos = 0x0DEC0114 / 0x0DEC0118 / 0x0DEC011C (README).
python -B mutants_rows.py [TSV]"""
import collections, os, sys
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import mutants_vectors as MV

M32 = 0xFFFFFFFF
DECOY = {'x': 0x0DEC0114, 'y': 0x0DEC0118, 'z': 0x0DEC011C}
AX = {'6B': 'x', '6C': 'y', '6D': 'z'}
PI = {'x': 0, 'y': 1, 'z': 2}


def correct(op, instr, pos, memo, result_in, owner=DECOY):
    if op == '6A':
        return result_in, (pos['x'], pos['y'], pos['z'])
    ax = AX[op]
    return MV.correct(instr, pos[ax], memo[PI[ax]]), tuple(memo)


def mut_6a_forgets_z(op, instr, pos, memo, result_in, owner=DECOY):
    if op == '6A':
        return result_in, (pos['x'], pos['y'], memo[2])
    return correct(op, instr, pos, memo, result_in)


def mut_6a_forgets_y(op, instr, pos, memo, result_in, owner=DECOY):
    if op == '6A':
        return result_in, (pos['x'], memo[1], pos['z'])
    return correct(op, instr, pos, memo, result_in)


def mut_6a_writes_result(op, instr, pos, memo, result_in, owner=DECOY):
    if op == '6A':
        return 1, (pos['x'], pos['y'], pos['z'])
    return correct(op, instr, pos, memo, result_in)


def mut_6a_clears_result(op, instr, pos, memo, result_in, owner=DECOY):
    if op == '6A':
        return 0, (pos['x'], pos['y'], pos['z'])
    return correct(op, instr, pos, memo, result_in)


def mut_reads_owner(op, instr, pos, memo, result_in, owner=DECOY):
    if op == '6A':
        return result_in, (owner['x'], owner['y'], owner['z'])
    ax = AX[op]
    return MV.correct(instr, owner[ax], memo[PI[ax]]), tuple(memo)


def mut_6d_reads_y(op, instr, pos, memo, result_in, owner=DECOY):
    if op == '6D':
        return MV.correct(instr, pos['y'], memo[1]), tuple(memo)
    return correct(op, instr, pos, memo, result_in)


def mut_6d_reads_p3(op, instr, pos, memo, result_in, owner=DECOY):
    if op == '6D':
        return MV.correct(instr, pos['z'], memo[1]), tuple(memo)
    return correct(op, instr, pos, memo, result_in)


def mut_6b_reads_z_memo(op, instr, pos, memo, result_in, owner=DECOY):
    if op == '6B':
        return MV.correct(instr, pos['x'], memo[2]), tuple(memo)
    return correct(op, instr, pos, memo, result_in)


def mut_no_write_on_zero(op, instr, pos, memo, result_in, owner=DECOY):
    if op in AX:
        r, m = correct(op, instr, pos, memo, result_in)
        return (result_in if r == 0 else r), m
    return correct(op, instr, pos, memo, result_in)


def mut_no_write_on_one(op, instr, pos, memo, result_in, owner=DECOY):
    if op in AX:
        r, m = correct(op, instr, pos, memo, result_in)
        return (result_in if r == 1 else r), m
    return correct(op, instr, pos, memo, result_in)


def mut_reads_threshold_signed_byte(op, instr, pos, memo, result_in, owner=DECOY):
    if op in AX:
        b2 = instr[2] - 256 if instr[2] >= 128 else instr[2]       # (sbyte) cast of v[2]
        ax = AX[op]
        d = MV.i32(MV.i32(memo[PI[ax]]) - MV.i32(pos[ax]))
        if d < 0:
            d = MV.i32(-d)
        thr = MV.i32((instr[1] | (b2 << 8)) << 16)
        return (0 if d < thr else 1), tuple(memo)
    return correct(op, instr, pos, memo, result_in)


MUTANTS = [
    ('6A forgets Parameters[4] (Z)', mut_6a_forgets_z),
    ('6A forgets Parameters[3] (Y)', mut_6a_forgets_y),
    ('6A writes Result := 1', mut_6a_writes_result),
    ('6A writes Result := 0', mut_6a_clears_result),
    ('reads the owner instead of the logic entity (all four)', mut_reads_owner),
    ('6D reads the Y axis', mut_6d_reads_y),
    ('6D reads Parameters[3] with PosZ (M11)', mut_6d_reads_p3),
    ('6B reads the Z memo', mut_6b_reads_z_memo),
    ('Result not written when it would be 0', mut_no_write_on_zero),
    ('Result not written when it would be 1', mut_no_write_on_one),
    ('v[2] cast to sbyte', mut_reads_threshold_signed_byte),
]


def main():
    path = sys.argv[1] if len(sys.argv) > 1 else os.path.join(HERE, '..', 'vectors', 'vectors-sm2.tsv')
    rows = [l.rstrip('\n').split('\t') for l in open(path, encoding='utf-8')]
    head, rows = rows[0], rows[1:]
    col = {n: i for i, n in enumerate(head)}
    print('rows:', len(rows))
    for name, fn in MUTANTS:
        killed = collections.Counter()
        total = 0
        first = None
        for r in rows:
            op = r[col['op']]
            instr = [int(b, 16) for b in r[col['instr']].split()]
            pos = {'x': int(r[col['pos_x']], 16), 'y': int(r[col['pos_y']], 16), 'z': int(r[col['pos_z']], 16)}
            memo = (int(r[col['memo_p2']], 16), int(r[col['memo_p3']], 16), int(r[col['memo_p4']], 16))
            rin = int(r[col['result_in']], 16)
            want = (int(r[col['result_out']], 16), (int(r[col['memo_p2_out']], 16), int(r[col['memo_p3_out']], 16), int(r[col['memo_p4_out']], 16)))
            assert correct(op, instr, pos, memo, rin) == want, r[0]
            got = fn(op, instr, pos, memo, rin)
            if got != want:
                killed[(op, r[col['group']])] += 1
                total += 1
                first = first or r[0]
        by_op = collections.Counter()
        for (op, g), n in killed.items():
            by_op[op] += n
        print('  %-58s killed by %4d rows (first %s) by op %s' % (name, total, first, dict(sorted(by_op.items()))))
        if total == 0:
            print('     !!! SURVIVES')


if __name__ == '__main__':
    main()
