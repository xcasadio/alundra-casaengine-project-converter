"""a3_edges.py - disc-e20g3 A3: the exact edges of the Y window of the running jump onto rec5, to one unit (1/65536 px) of PosY.
For each side and each y (PosY raw units = 65536*296 + dy), tj in 11..17: success = the hero stands on rec5 (support == rec5, ground) at some tick."""
import sys, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import multiprocessing as mp


def one(a):
    import lib392 as L
    side, yraw, tj = a
    DIR = L.LEFT if side == 'east' else L.RIGHT
    xs = 720 if side == 'east' else 560
    m, S = L.world392(hero_px=(xs, 296), direction=(8 if side == 'east' else 24))
    h = L.K.ent(0)
    m.poke(h + 0x118, yraw & 0xFFFFFFFF)
    pads, n = L.pad_seq([(tj - 1, DIR), (1, DIR | L.CROSS), (70 - tj, DIR)])
    rows = L.gl.run(m, n, pads, crates=list(S.values()))
    s5 = L.K.ent(S[5])
    ok = any(r['hero']['support'] == s5 and r['hero']['ground'] for r in rows)
    ypos = rows[min(len(rows) - 1, 20)]['hero']['py']
    return side, yraw, tj, ok, ypos


if __name__ == '__main__':
    base = 296 * 65536
    jobs = []
    for side in ('east', 'west'):
        for dy_edge in (297 * 65536, 295 * 65536):
            for k in (-3, -2, -1, 0, 1, 2, 3):
                for tj in range(11, 18):
                    jobs.append((side, dy_edge + k, tj))
    with mp.Pool(22) as p:
        res = p.map(one, jobs)
    from collections import defaultdict
    d = defaultdict(list)
    for side, yraw, tj, ok, ypos in res:
        d[(side, yraw)].append((tj, ok, ypos))
    lines = []
    for (side, yraw) in sorted(d):
        oks = [tj for tj, ok, _ in d[(side, yraw)] if ok]
        lines.append('%-5s PosY=%d (= %.7f px = edge %+d units) : success for %d of 7 jump ticks %s ; PosY at tick 21 = %d' % (side, yraw, yraw / 65536, yraw - round(yraw / 65536) * 65536, len(oks), oks, d[(side, yraw)][0][2]))
    open(os.path.join(os.path.dirname(os.path.abspath(__file__)), 'out', 'a3_edges.txt'), 'w').write('\n'.join(lines) + '\n')
    print('\n'.join(lines))
