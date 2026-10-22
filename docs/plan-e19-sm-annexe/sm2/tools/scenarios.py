"""The real-bytes scenarios of the sm2 tests, derived with mini_prog.py (second method). python -B scenarios.py > ../evidence/scenarios.txt"""
import os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import mini_prog as P

PX = lambda px: (px << 16) & 0xFFFFFFFF

SCENARIOS = [
    # name, map, slot, prog, start_pc, result0, ticks, flags
    ('RB-55  55 C[4]  X patrol 120 px (grow)', 55, 'C', 4, None, 1,
     [dict(pos=(PX(64), PX(200), PX(8))), dict(pos=(PX(64 + 119), PX(200), PX(8))), dict(pos=(PX(64 + 120), PX(200), PX(8)))]),
    ('RB-55n 55 C[4]  X patrol 120 px (shrink: abs)', 55, 'C', 4, None, 1,
     [dict(pos=(PX(200), PX(200), PX(8))), dict(pos=(PX(200 - 119), PX(200), PX(8))), dict(pos=(PX(200 - 120), PX(200), PX(8)))]),
    ('RB-56  56 C[1]  6D without 6A, P4 = 0, threshold 256 px (from pc 93)', 56, 'C', 1, 93, 1,
     [dict(pos=(PX(96), PX(96), 0x00FFFFFF)), dict(pos=(PX(96), PX(96), 0x00FFFFFF)), dict(pos=(PX(96), PX(96), 0x01000000))]),
    ('RB-56b 56 C[1]  6D without 6A, Result0 = 0 (the arrival tick)', 56, 'C', 1, 93, 0,
     [dict(pos=(PX(96), PX(96), 0x00FFFFFF)), dict(pos=(PX(96), PX(96), 0x01000000))]),
    ('RB-59  59 C[6]  Z balloon, 32 px, two 6A', 59, 'C', 6, None, 1,
     [dict(pos=(PX(80), PX(60), PX(96))), dict(pos=(PX(80), PX(60), PX(96))), dict(pos=(PX(80), PX(60), PX(96 + 32)))]),
    ('RB-307 307 C[1] memo across a 0x78 call', 307, 'C', 1, None, 1,
     [dict(pos=(PX(37), PX(35), PX(40))), dict(pos=(PX(37 + 59), PX(35), PX(40))), dict(pos=(PX(37 + 60), PX(35), PX(40)))]),
    ('RB-344 344 C[60] Y 64 px then X 96 px (two memos)', 344, 'C', 60, None, 1,
     [dict(pos=(PX(88), PX(512), PX(96))), dict(pos=(PX(88), PX(512 + 63), PX(96))), dict(pos=(PX(88), PX(512 + 64), PX(96))), dict(pos=(PX(88), PX(512 + 64), PX(96))),
      dict(pos=(PX(88 + 95), PX(512 + 64), PX(96))), dict(pos=(PX(88 + 96), PX(512 + 64), PX(96)))]),
    ('RB-442 442 C[3]  X patrol 72 px', 442, 'C', 3, None, 1,
     [dict(pos=(PX(192), PX(100), PX(64))), dict(pos=(PX(192 + 71), PX(100), PX(64))), dict(pos=(PX(192 + 72), PX(100), PX(64)))]),
]

if __name__ == '__main__':
    for name, mid, slot, prog, start, result0, ticks in SCENARIOS:
        print('=== %s (result0=%d)' % (name, result0))
        for mode in ('port', 'today'):
            res = P.simulate(mode, mid, slot, prog, start, result0, ticks)
            print('  [%s]' % mode)
            print(P.fmt(res))
