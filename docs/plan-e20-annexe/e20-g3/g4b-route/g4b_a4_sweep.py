"""g4b_a4_sweep.py - E20.g4b T0 (a4): the Cross tick tj of the airlift, every tj from 8 to 26 and y = 298 (the start of (b4)), on the binary with the 12 records: the gate of the oracle (w5_airlift.py) = the Square at the first tick that follows a
tick that ended with a contact and the hero in the air.  Reports the Square tick, the contact record, the hero z, what is carried.  Output out/g4b_a4_sweep.txt"""
import os
import sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import multiprocessing as mp
import g4b_lib as L
G = L.G
U = L.U


def one(a):
    y, tj = a
    s = L.Sim((720, int(y)), 8, x_raw=720 * U, y_raw=int(round(y * U)))
    sq = None
    res = None
    for _ in range(80):
        h = s.hero()
        t = s.t + 1
        hold = G.LEFT | (G.CROSS if t == tj else 0)
        if sq is None and t > tj and h['xcoll'] is not None and h['ground'] == 0:
            hold |= G.SQUARE
            sq = (t, h['xcoll'], round(h['z'], 3))
        s.tick(hold)
        if sq and res is None:
            res = (s.rows[-1]['carried'], s.rows[-1]['anim'])
    return y, tj, sq, res


if __name__ == '__main__':
    mp.freeze_support()
    jobs = [(y, tj) for y in (298.0,) for tj in range(8, 27)]
    with mp.Pool(16) as p:
        R = p.map(one, jobs)
    out = ['E20.g4b T0 (a4) - Cross tick sweep, start (720, y, z 64) facing west, Left held, Cross at tick tj (counted from the pose), Square at the first tick that follows a tick ended with a contact and the hero in the air; 12 records (binary)',
           'y tj -> (Square tick, contact record, hero z) -> (carried record, hero animation after)']
    for y, tj, sq, res in R:
        out.append('  y %.1f tj %2d -> %s -> %s' % (y, tj, sq, res))
    txt = '\n'.join(out) + '\n'
    open(os.path.join(G.OUT, 'g4b_a4_sweep.txt'), 'w', encoding='utf-8').write(txt)
    print(txt)
