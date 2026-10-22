"""Mutants of the 0x6B/6C/6D formula evaluated over the frozen vectors (no binary needed): how many rows kill each mutant, by group.
A mutant 'throws' when the C# it stands for would raise (Math.Abs on int.MinValue). python -B mutants_vectors.py [TSV]"""
import collections, os, sys

HERE = os.path.dirname(os.path.abspath(__file__))
M32 = 0xFFFFFFFF


def i32(x):
    x &= M32
    return x - (1 << 32) if x & 0x80000000 else x


def correct(instr, pos, memo):
    d = i32(i32(memo) - i32(pos))
    if d < 0:
        d = i32(-d)
    thr = i32((instr[1] | (instr[2] << 8)) << 16)
    return 0 if d < thr else 1


class Throws(Exception):
    pass


def m_math_abs(instr, pos, memo):
    d = i32(i32(memo) - i32(pos))
    if d == -(1 << 31):
        raise Throws()
    d = abs(d)
    thr = i32((instr[1] | (instr[2] << 8)) << 16)
    return 0 if d < thr else 1


def m_pixel_shift(instr, pos, memo):          # the form of Walk/WaitZDistance: (|d| >> 16) >= (v1 | v2 << 8)
    d = i32(i32(memo) - i32(pos))
    if d < 0:
        d = i32(-d)
    return 1 if ((d & M32) >> 16) >= (instr[1] | (instr[2] << 8)) else 0


def m_unsigned_threshold(instr, pos, memo):   # (uint) threshold and unsigned distance
    d = i32(i32(memo) - i32(pos))
    if d < 0:
        d = i32(-d)
    thr = ((instr[1] | (instr[2] << 8)) << 16) & M32
    return 0 if (d & M32) < thr else 1


def m_less_equal(instr, pos, memo):           # <= instead of <
    d = i32(i32(memo) - i32(pos))
    if d < 0:
        d = i32(-d)
    thr = i32((instr[1] | (instr[2] << 8)) << 16)
    return 0 if d <= thr else 1


def m_inverted(instr, pos, memo):             # the table's old reading: Result = d < thr
    return 1 - correct(instr, pos, memo)


def m_long_subtraction(instr, pos, memo):     # no 32-bit wrap: (long) arithmetic
    d = abs(i32(memo) - i32(pos))
    thr = i32((instr[1] | (instr[2] << 8)) << 16)
    return 0 if d < thr else 1


def m_no_abs(instr, pos, memo):               # signed difference, no absolute value
    d = i32(i32(memo) - i32(pos))
    thr = i32((instr[1] | (instr[2] << 8)) << 16)
    return 0 if d < thr else 1


def m_swapped_operands(instr, pos, memo):     # pos - memo (the same absolute value, but the INT_MIN wrap differs)
    d = i32(i32(pos) - i32(memo))
    if d < 0:
        d = i32(-d)
    thr = i32((instr[1] | (instr[2] << 8)) << 16)
    return 0 if d < thr else 1


def m_threshold_high_byte_only(instr, pos, memo):   # threshold read from byte 1 only (byte 2 ignored)
    d = i32(i32(memo) - i32(pos))
    if d < 0:
        d = i32(-d)
    thr = i32(instr[1] << 16)
    return 0 if d < thr else 1


def m_threshold_big_endian(instr, pos, memo):       # (v1 << 8 | v2) << 16
    d = i32(i32(memo) - i32(pos))
    if d < 0:
        d = i32(-d)
    thr = i32(((instr[1] << 8) | instr[2]) << 16)
    return 0 if d < thr else 1


def m_clamped_abs(instr, pos, memo):                # abs that saturates int.MinValue to int.MaxValue
    d = i32(i32(memo) - i32(pos))
    if d < 0:
        d = i32(-d)
    if d == -(1 << 31):
        d = (1 << 31) - 1
    thr = i32((instr[1] | (instr[2] << 8)) << 16)
    return 0 if d < thr else 1


MUTANTS = [
    ('Math.Abs (throws on int.MinValue)', m_math_abs),
    ('pixel form (|d| >> 16) >= (v1 | v2 << 8)', m_pixel_shift),
    ('unsigned threshold and distance', m_unsigned_threshold),
    ('<= instead of <', m_less_equal),
    ('Result inverted (the table label)', m_inverted),
    ('64-bit subtraction (no wrap)', m_long_subtraction),
    ('no absolute value', m_no_abs),
    ('operands swapped (pos - memo)', m_swapped_operands),
    ('threshold from byte 1 only', m_threshold_high_byte_only),
    ('threshold bytes big-endian', m_threshold_big_endian),
    ('abs saturates int.MinValue', m_clamped_abs),
]


def main():
    path = sys.argv[1] if len(sys.argv) > 1 else os.path.join(HERE, '..', 'vectors', 'vectors-sm2.tsv')
    rows = [l.rstrip('\n').split('\t') for l in open(path, encoding='utf-8')]
    head, rows = rows[0], rows[1:]
    col = {n: i for i, n in enumerate(head)}
    rows = [r for r in rows if r[col['op']] in ('6B', '6C', '6D')]
    print('rows used (0x6B/6C/6D):', len(rows))
    for name, fn in MUTANTS:
        killed = collections.Counter()
        total = 0
        first = None
        for r in rows:
            op = r[col['op']]
            ax = {'6B': 'x', '6C': 'y', '6D': 'z'}[op]
            instr = [int(b, 16) for b in r[col['instr']].split()]
            pos = int(r[col['pos_' + ax]], 16)
            memo = int(r[col['memo_p' + {'x': '2', 'y': '3', 'z': '4'}[ax]]], 16)
            want = int(r[col['result_out']], 16)
            assert correct(instr, pos, memo) == want, r[0]
            try:
                got = fn(instr, pos, memo)
                dead = got != want
            except Throws:
                dead = True
            if dead:
                killed[r[col['group']]] += 1
                total += 1
                if first is None:
                    first = r[0]
        print('  %-48s killed by %4d rows (first %s)  by group %s' % (name, total, first, dict(killed)))
        if total == 0:
            print('     !!! SURVIVES: the vectors do not distinguish it')


if __name__ == '__main__':
    main()
