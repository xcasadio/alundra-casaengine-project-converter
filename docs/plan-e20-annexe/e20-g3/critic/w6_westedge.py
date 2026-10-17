"""w6_westedge.py - critic: the window edges from the WEST in the REAL map (all 12 crates, so rec9 at (576, 288) is present): the hero starts at
(600, y) on the floor, clear of rec9 (hero box X [590, 611) vs rec9 [566, 587)), holds RIGHT, Cross at tick tj (1..12), RIGHT to tick 70.
Success = some tick with support == rec5 and ground.  Also reported: the hero ends east of x 657 (past the column).  Usage: python -B -I w6_westedge.py"""
import os
import sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import multiprocessing as mp

U = 65536
YS = [294 * U + U // 2, 295 * U - 1, 295 * U, 296 * U, 297 * U, 297 * U + 1, 298 * U]


def one(a):
    import my392 as G
    y, tj = a
    m, S = G.world((600, 296), 24, yraw=y)
    s5 = G.K.ent(S[5])
    rows = G.run(m, [(tj - 1, G.RIGHT), (1, G.RIGHT | G.CROSS), (70 - tj, G.RIGHT)], S)
    rec_of = {G.K.ent(s): r for r, s in S.items()}
    start_sup = rec_of.get(rows[0]['hero']['support'])
    on5 = [r['tick'] for r in rows if r['hero']['support'] == s5 and r['hero']['ground']]
    other = sorted({rec_of[r['hero']['support']] for r in rows if r['hero']['support'] in rec_of and r['hero']['support'] != s5})
    fx = rows[-1]['hero']['px'] / U
    return y, tj, start_sup, bool(on5), other, fx > 657


if __name__ == '__main__':
    mp.freeze_support()
    jobs = [(y, tj) for y in YS for tj in range(1, 13)]
    with mp.Pool(22) as p:
        R = p.map(one, jobs)
    out = ['WEST start (600, y), floor, 12 crates: y -> support at tick 1, rec5 landings k/12 [tj], other crates stood on, ended past the column k/12']
    for y in YS:
        rr = [r for r in R if r[0] == y]
        out.append('  y %.7f : start support %s ; on rec5 %2d/12 %s ; other supports %s ; past %2d/12' % (
            y / U, rr[0][2], sum(r[3] for r in rr), [r[1] for r in rr if r[3]], sorted({o for r in rr for o in r[4]}), sum(r[5] for r in rr)))
    txt = '\n'.join(out) + '\n'
    open(os.path.join(os.path.dirname(os.path.abspath(__file__)), 'out', 'w6_westedge.txt'), 'w').write(txt)
    print(txt)
