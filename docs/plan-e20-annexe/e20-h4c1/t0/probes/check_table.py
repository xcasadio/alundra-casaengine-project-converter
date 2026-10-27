"""check_table.py (h4c1 T0): reads the table 0x8002343C of ALUN_CD.EXE (32 entries of 6 bytes: u16 tick, s16 speed, s16 delta, sentinel 0xFFFF at 0x800234FC) with exelib (read-only) and compares it with the table of the
throwaway probe (two entries per tick 20..95 by 5, speed 1024 up to the tick 55 then 1280, the sixteen deltas twice). usage: python -B check_table.py DIR_OF_EXELIB"""
import sys
sys.path.insert(0, sys.argv[1])
import exelib
seq = [3, -6, 0, 6, 3, 2, 8, 14, 3, 10, 16, 22, 3, 18, 24, 30]
bad = 0
for i in range(32):
    a = 0x8002343C + 6 * i
    t = exelib.u8(a) | exelib.u8(a + 1) << 8
    v, d = exelib.s16(a + 2), exelib.s16(a + 4)
    tick = 20 + 5 * (i // 2)
    want = (tick, 1024 if tick <= 55 else 1280, seq[i % 16])
    if (t, v, d) != want:
        bad += 1
        print('DIFF entry', i, (t, v, d), 'probe', want)
sent = exelib.u8(0x8002343C + 192) | exelib.u8(0x8002343C + 193) << 8
print('sentinel word', hex(sent), 'differences', bad)
sys.exit(1 if bad or sent != 0xFFFF else 0)
