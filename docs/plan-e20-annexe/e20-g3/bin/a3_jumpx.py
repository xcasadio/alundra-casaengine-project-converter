"""a3_jumpx.py - disc-e20g3 A3: for which hero X at the Cross press does the running jump (y=296) land on rec5, both sides? Output out/a3_jumpx.txt"""
import sys, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import multiprocessing as mp

def one(a):
    import lib392 as L
    side, tj = a
    DIR = L.LEFT if side == 'east' else L.RIGHT
    xs = 720 if side == 'east' else 560
    m, S = L.world392(hero_px=(xs, 296), direction=(8 if side == 'east' else 24))
    pads, n = L.pad_seq([(tj - 1, DIR), (1, DIR | L.CROSS), (70 - tj, DIR)])
    rows = L.gl.run(m, n, pads, crates=list(S.values()))
    s5 = L.K.ent(S[5])
    land = next((r['tick'] for r in rows if r['hero']['support'] == s5 and r['hero']['ground']), None)
    x_at = rows[tj - 2]['hero']['px'] / 65536 if tj >= 2 else xs     # position after the tick before the jump tick = the position when Cross is pressed
    blocked = next((r['tick'] for r in rows if r['hero']['xcoll']), None)
    return side, tj, x_at, land, rows[-1]['hero']['px'] / 65536, rows[-1]['hero']['pz'] / 65536, blocked

if __name__ == '__main__':
    jobs = [(s, tj) for s in ('east', 'west') for tj in range(6, 30)]
    with mp.Pool(22) as p:
        res = p.map(one, jobs)
    lines = []
    for side, tj, x_at, land, fx, fz, blocked in res:
        lines.append('%-4s Cross pressed at tick %2d with hero x=%8.3f : lands on rec5 at tick %-5s ; final x=%8.2f z=%6.2f ; first contact tick %s' % (side, tj, x_at, land, fx, fz, blocked))
    open(os.path.join(os.path.dirname(os.path.abspath(__file__)), 'out', 'a3_jumpx.txt'), 'w').write('\n'.join(lines) + '\n')
    print('\n'.join(lines))
