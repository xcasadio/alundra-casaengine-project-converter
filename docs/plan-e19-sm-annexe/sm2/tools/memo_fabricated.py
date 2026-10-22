"""Made-up programs for the memo-flow function (test S1-f): the expected abstract states per reader, from the reference implementation
cfg2.dataflow with the superset writer sets. python -B memo_fabricated.py > ../evidence/memo_fabricated.txt"""
import os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import corpus2 as C, cfg2, memo_static as M

W = M.CLOB_BIN
READERS = M.READERS


def s16b(n):
    n &= 0xFFFF
    return [n & 0xFF, n >> 8]


CASES = []


def case(name, codes, entry=0):
    CASES.append((name, codes, entry))


# straight: save, read
case('save then X read', [0x6A, 0x6B, 0x10, 0x00, 0xFF])
# read only
case('X read with no save (fresh state)', [0x6B, 0x10, 0x00, 0xFF])
case('Z read with no save', [0x6D, 0x10, 0x00, 0xFF])
# overwritten by 0x37 (X only)
case('save; 0x37; X read -> clobbered', [0x6A, 0x37, 0x05, 0x6B, 0x10, 0x00, 0xFF])
case('save; 0x37; Z read -> saved (Z immune)', [0x6A, 0x37, 0x05, 0x6D, 0x10, 0x00, 0xFF])
case('save; 0x37; Y read -> saved (0x37 writes P2 only)', [0x6A, 0x37, 0x05, 0x6C, 0x10, 0x00, 0xFF])
case('save; 0x1E; Y read -> clobbered (0x1E writes P2, P3)', [0x6A, 0x1E, 0x10, 0x00, 0x6C, 0x10, 0x00, 0xFF])
case('save; 0x1E; Z read -> saved', [0x6A, 0x1E, 0x10, 0x00, 0x6D, 0x10, 0x00, 0xFF])
case('save; 0x5F (binary-only writer of P2..P6, size 8); Z read -> clobbered', [0x6A, 0x5F, 0, 0, 0, 0, 0, 0, 0, 0x6D, 0x10, 0x00, 0xFF])
# loop back to before the save: reader reachable with Init and Saved
case('read; save; goto entry (backward jump): the read is reached Init (first pass) and Saved (second)', [0x6B, 0x10, 0x00, 0x6A, 0x02] + s16b(-4) + [0xFF])
case('save at 0; loop 02 -> 1 reading X: Saved only', [0x6A, 0x6B, 0x10, 0x00, 0x02] + s16b(-4) + [0xFF])
case('branch: one arm saves, the other does not; read after the join -> Init and Saved',
     [0x03] + s16b(7) + [0x6A, 0x02] + s16b(3) + [0x6B, 0x10, 0x00, 0xFF])
# calls (the callee starts right after the final FF)
case('save; 0x78 call to an innocent callee (6E 7F 1B 7D); X read -> saved',
     [0x6A, 0x78] + s16b(7) + [0x6B, 0x10, 0x00, 0xFF, 0x6E, 0x7F, 0x1B, 0x00, 0x00, 0x7D])
case('save; 0x78 call to a callee with 0x37 (7D return); X read -> clobbered',
     [0x6A, 0x78] + s16b(7) + [0x6B, 0x10, 0x00, 0xFF, 0x37, 0x05, 0x7D])
# the corpus shape of map 307 C[1], reduced
case('map 307 shape: save; fly; call (6E 7F 1B 7D); read',
     [0x6A, 0x1B, 0x00, 0xFF, 0x78] + s16b(7) + [0x6B, 0x3C, 0x00, 0xFF, 0x6E, 0x7F, 0x1B, 0x00, 0x00, 0x7D])


def run():
    for name, codes, entry in CASES:
        df, seen, marks, ru = cfg2.dataflow(codes, entry, W, READERS)
        out = []
        for pc in sorted(df):
            for axis, st in df[pc].items():
                out.append('@%d %02X(%s) -> {%s}' % (pc, codes[pc], axis, ','.join(sorted(st))))
        print('%-78s codes=%s' % (name, ' '.join('%02X' % b for b in codes)))
        print('      reach=%d marks=%s : %s' % (len(seen), sorted(marks), '; '.join(out)))


if __name__ == '__main__':
    run()
