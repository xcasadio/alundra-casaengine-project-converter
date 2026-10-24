"""g4a_mouth.py - E20.g4a T0 (d): the alignment of the hero at the mouth of the corridor of the portal 0 (cell (22, 23)), on the REAL code of ALUN_CD.EXE (France), my392 harness (12 records).
Start (x0, 344) at z 64, Down held 14 ticks, x0 = 534.0 .. 541.5 by 0.0625 (121 starts).  Reported: the number of ticks that moved x with y unchanged (the second steps along the wall, 0.75 px each) before the
first tick that moved y, the x at that tick, and the tick.  Nothing is written after the start.  Usage: python -B -I g4a_mouth.py"""
import os
import sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import my392 as G

U = 65536
out = ['binary: start (x0, 344, z 64), DOWN held 14 ticks: x0 -> (ticks that moved x with y unchanged before y first moved, x when y first moves, tick)']
for n in range(121):
    x0 = 534.0 + n * 0.0625
    m, S = G.world((int(x0), 344), 0, xraw=int(round(x0 * U)), yraw=344 * U)
    rows = G.run(m, [(14, G.DOWN)], S)
    px, py = x0, 344.0
    steps = 0
    first = None
    for r in rows:
        nx, ny = r['hero']['px'] / U, r['hero']['py'] / U
        if ny != py:
            first = (nx, r['tick'])
            break
        if nx != px:
            steps += 1
        px = nx
    out.append('x0 %.4f -> %s' % (x0, 'steps %d, x %.4f, tick %d' % (steps, first[0], first[1]) if first else 'never moved in y (steps %d, x %.4f)' % (steps, px)))
open(os.path.join(os.path.dirname(os.path.abspath(__file__)), 'out', 'g4a_mouth_binary.txt'), 'w').write('\n'.join(out) + '\n')
print('\n'.join(out[:6]))
print('...')
print('\n'.join(out[-3:]))
