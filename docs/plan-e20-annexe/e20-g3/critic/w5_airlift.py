"""w5_airlift.py - critic: can the hero lift a crate in the AIR (PlayerTryAction is called from the jump states, 0x80031E98)?
Start (720, y) facing west, hold LEFT, Cross at tick tj; the first tick after a tick that ended with contact +0x130 != 0 AND the hero not on the ground
(+0x144 == 0), press SQUARE (LEFT held).  Report the contact record, what is carried (+0x2C), the hero state after (+0x88).  Real code, 12 crates.
Usage: python -B -I w5_airlift.py"""
import os
import sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import multiprocessing as mp

U = 65536


def one(a):
    import my392 as G
    W = G.gl.W
    y, tj = a
    m, S = G.world((720, 296), 8, yraw=int(round(y * U)))
    K = G.K
    h = K.ent(0)
    rec_of = {K.ent(s): r for r, s in S.items()}
    pad = W.Pad()
    res = None
    sq_t = None
    for t in range(1, 70):
        xc, gr = m.peek(h + 0x130), m.peeks(h + 0x144)
        hold = G.LEFT | (G.CROSS if t == tj else 0)
        if sq_t is None and t > tj and xc in rec_of and gr == 0:
            hold |= G.SQUARE
            sq_t = (t, rec_of[xc], m.peeks(h + 0x11C) / U, m.peeks(h + 0x88))
        m.tick_no = t
        W.tick(m, pad, hold)
        if sq_t and res is None:
            res = (rec_of.get(m.peek(h + 0x2C)), m.peeks(h + 0x88))
    return y, tj, sq_t, res


if __name__ == '__main__':
    mp.freeze_support()
    jobs = [(y, tj) for y in (298.0, 300.0, 312.0, 290.0) for tj in range(10, 24, 2)]
    with mp.Pool(22) as p:
        R = p.map(one, jobs)
    out = ['y, Cross tick -> (Square tick, contact rec at the previous tick end, hero z, hero state before) -> (carried rec, hero state after)']
    for y, tj, sq, res in R:
        out.append('  y %.1f tj %2d -> %s -> %s' % (y, tj, sq, res))
    txt = '\n'.join(out) + '\n'
    open(os.path.join(os.path.dirname(os.path.abspath(__file__)), 'out', 'w5_airlift.txt'), 'w').write(txt)
    print(txt)
