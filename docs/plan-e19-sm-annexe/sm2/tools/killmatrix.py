"""Kill matrix: each hand-written test of the plan (H1..H9, RB-*) is an observation; a mutant is KILLED by a test when the observation differs
from the one of the port. The vector tests V6A..V6D are the row-level evaluations of mutants_rows.py / mutants_vectors.py (aggregated).
python -B killmatrix.py > ../evidence/killmatrix.txt
"""
import collections, os, sys
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import mini_prog as P
import scenarios as S
import mutants_rows as MR
import mutants_vectors as MV

PX = lambda px: (px << 16) & 0xFFFFFFFF
FILL = (0x11111111, 0x22222222, 0x33333333)

MODES = [
    ('M1  Math.Abs on the distance (throws on int.MinValue)', 'mut-math-abs', 'R2 32-bit wrap'),
    ('M2  pixel form (|d| >> 16) >= (v1 | v2 << 8)', 'mut-pixel-form', 'R2 threshold/sign'),
    ('M3  unsigned threshold and distance', 'mut-unsigned-threshold', 'R2 threshold/sign'),
    ('M4  <= instead of < (equality)', 'mut-less-equal', 'R2 equality'),
    ('M5  Result inverted (the old label)', 'mut-inverted', 'R2 sense'),
    ('M6  64-bit subtraction (no wrap)', 'mut-long-subtraction', 'R2 32-bit wrap'),
    ('M7  no absolute value', 'mut-no-abs', 'R2 abs'),
    ('M8  0x6A forgets Parameters[4]', 'mut-6a-forgets-z', 'R1 three axes'),
    ('M9  0x6A writes Result := 1', 'mut-6a-writes-result', 'R1/R4 Result untouched'),
    ('M10 reads the OWNER instead of the logic entity', 'mut-reads-owner', 'R1/R2 logic entity'),
    ('M11 0x6D reads Parameters[3]', 'mut-6d-reads-p3', 'R3 Z memo cell'),
    ('M12 0x6B returns 1 instead of 3', 'mut-6b-size-1', 'R5 sizes'),
    ('M13 0x6A returns 2 instead of 1', 'mut-6a-size-2', 'R5 sizes'),
    ('M14 Break clears the memo', 'mut-break-clears-memo', 'R3 memo survives Break'),
    ('M15 InitializeEventData clears the memo', 'mut-init-clears-memo', 'R3 memo survives re-init'),
    ('M16 the Wait (0x37) keeps its counter out of the X memo cell', 'mut-wait-spares-p2', 'R3 faithful overwrite'),
]


def cases():
    out = []

    def one(name, codes, ticks, params=None, result0=7):
        out.append((name, codes, ticks, params, result0))

    # H1 - 0x6A copies the three positions, Result alone
    one('H1  Save copies the three positions of the logic entity, Result stays 7', [0x6A, 0xFF], [dict(pos=(0x180000, 0x280000, 0x300000))], [0, 0, *FILL, 0, 0, 0, 0, 0])
    # H2 - threshold equality and sign, per axis
    for label, op, thr, memo, poss in (
        ('X', 0x6B, (0x78, 0x00), 0x00100000, (0x00880000, 0x0087FFFF, 0x00880001)),
        ('X<', 0x6B, (0x78, 0x00), 0x00880000, (0x00100000, 0x00100001, 0x0010FFFF)),
        ('Y', 0x6C, (0x10, 0x00), 0x02000000, (0x02100000, 0x020FFFFF)),
        ('Z', 0x6D, (0x01, 0x00), 0x00600000, (0x00610000, 0x0060FFFF)),
    ):
        for pos in poss:
            params = [0, 0, memo if op == 0x6B else 0, memo if op == 0x6C else 0, memo if op == 0x6D else 0, 0, 0, 0, 0, 0]
            one('H2%s pos=%08X (memo %08X, thr %02X%02X)' % (label, pos, memo, thr[1], thr[0]), [op, thr[0], thr[1], 0xFF],
                [dict(pos=(pos, pos, pos))], params)
    # H3 - thresholds from 0x8000 are negative
    for name, instr, memo, pos in (
        ('H3a 6B 00 80  memo=pos', [0x6B, 0x00, 0x80, 0xFF], 0x00500000, 0x00500000),
        ('H3b 6B FF FF  memo=0x80000000 pos=0 (d = int.MinValue)', [0x6B, 0xFF, 0xFF, 0xFF], 0x80000000, 0),
        ('H3c 6B 00 80  memo=0x80000000 pos=0 (d = thr = int.MinValue)', [0x6B, 0x00, 0x80, 0xFF], 0x80000000, 0),
    ):
        one(name, instr, [dict(pos=(pos, 0, 0))], [0, 0, memo, 0, 0, 0, 0, 0, 0, 0])
    # H4 - the subtraction wraps, no exception
    one('H4  6B 78 00  memo=0x7FFFFFFF pos=0xFFFFFFFF (memo - pos wraps to int.MinValue)', [0x6B, 0x78, 0x00, 0xFF],
        [dict(pos=(0xFFFFFFFF, 0, 0))], [0, 0, 0x7FFFFFFF, 0, 0, 0, 0, 0, 0, 0])
    # H5 - 0x6D with no 0x6A: a fresh state compares 0 to PosZ
    for pos in (0x00FFFFFF, 0x01000000, 0x01000001):
        one('H5  fresh 6D 00 01  PosZ=%08X' % pos, [0x6D, 0x00, 0x01, 0xFF], [dict(pos=(0, 0, pos))])
    # H7 - the memo survives a Break and the next tick
    brk = [0x6A, 0x00, 0x6B, 0x78, 0x00, 0xFF]
    one('H7a save; Break; read after +120 px', brk, [dict(pos=(PX(64), 0, 0)), dict(pos=(PX(64 + 120), 0, 0))])
    one('H7b save; Break; read after +119 px', brk, [dict(pos=(PX(64), 0, 0)), dict(pos=(PX(64 + 119), 0, 0))])
    # H8 - the memo survives InitializeEventData
    one('H8  save; Break; InitializeEventData (no run); memo intact', brk, [dict(pos=(PX(64), PX(5), PX(6))), dict(pos=(PX(64), PX(5), PX(6)), reinit=True, norun=True)])
    # H9 - faithful overwrite by 0x37 (X) and immunity (Y, Z)
    for label, op in (('X', 0x6B), ('Y', 0x6C), ('Z', 0x6D)):
        ticks = [dict(pos=(0x00500000, 0x00500000, 0x00500000)) for _ in range(5)]
        one('H9%s save; Wait 3; read (unmoved entity)' % label, [0x6A, 0x37, 0x03, op, 0x10, 0x00, 0xFF], ticks, result0=7)
    return out


def observe(mode, codes, ticks, params, result0):
    try:
        return P.simulate_codes(mode, codes, 0, None, result0, ticks, params=params)
    except (P.Unmodelled, IndexError) as e:
        return ('CRASH', type(e).__name__)


def observe_real(mode, mid, slot, prog, start, result0, ticks):
    try:
        return P.simulate(mode, mid, slot, prog, start, result0, ticks)
    except (P.Unmodelled, IndexError) as e:
        return ('CRASH', type(e).__name__)


def main():
    hand = cases()
    # real-bytes scenarios
    real = []
    for name, mid, slot, prog, start, result0, ticks in S.SCENARIOS:
        real.append((name.split()[0] + ' ' + name.split()[1] + ' ' + name.split()[2], (mid, slot, prog, start, result0, ticks)))
    print('hand tests: %d ; real-bytes scenarios: %d' % (len(hand), len(real)))
    base = {}
    for name, codes, ticks, params, result0 in hand:
        base[name] = observe('port', codes, ticks, params, result0)
    today = {name: observe('today', codes, ticks, params, result0) for name, codes, ticks, params, result0 in hand}
    rbase = {}
    for rn, (mid, slot, prog, start, result0, ticks) in real:
        rbase[rn] = P.simulate('port', mid, slot, prog, start, result0, ticks)
    print('\nRED today (the skip by size), hand tests:')
    for name in base:
        print('   %-82s %s' % (name, 'RED' if today[name] != base[name] else 'green today (a guard)'))
    print('\nRED today, real-bytes scenarios:')
    for rn, (mid, slot, prog, start, result0, ticks) in real:
        t = P.simulate('today', mid, slot, prog, start, result0, ticks)
        print('   %-30s %s' % (rn, 'RED' if t != rbase[rn] else 'green today'))
    print('\nKILL MATRIX (mutant -> tests that go red):')
    for label, mode, rule in MODES:
        killed = []
        for name, codes, ticks, params, result0 in hand:
            if observe(mode, codes, ticks, params, result0) != base[name]:
                killed.append(name.split()[0])
        for rn, (mid, slot, prog, start, result0, ticks) in real:
            if observe_real(mode, mid, slot, prog, start, result0, ticks) != rbase[rn]:
                killed.append(rn.split()[0])
        print('  %-52s [%s]\n      killed by: %s' % (label, rule, ', '.join(sorted(set(killed), key=str)) or '*** NONE ***'))


if __name__ == '__main__':
    main()
