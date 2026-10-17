"""w4_contact.py - critic: when is the contact +0x130 set on the approach to rec5, compared with the tick the hero becomes flush (PosX = 657.0)?
Start (x0, 296) facing west, hold LEFT 40 ticks, x0 over one full step period (2.4375 px) in 1/32 px steps, real code, 12 crates.
Then the lift rule: for each x0, SQUARE on (a) the tick the hero first becomes flush, (b) the next tick; report lifted or not (+0x2C).
Usage: python -B -I w4_contact.py"""
import os
import sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import multiprocessing as mp

U = 65536


def approach(x0):
    import my392 as G
    m, S = G.world((700, 296), 8, xraw=int(round(x0 * U)))
    s5 = G.K.ent(S[5])
    rows = G.run(m, [(40, G.LEFT)], S)
    flush = next((r['tick'] for r in rows if r['hero']['px'] == 657 * U), None)
    c = next((r['tick'] for r in rows if r['hero']['xcoll'] == s5), None)
    pre = [(r['tick'], r['hero']['px'] / U, r['hero']['xcoll'] == s5) for r in rows if flush and flush - 2 <= r['tick'] <= flush + 1]
    return x0, flush, c, pre


def lift_at(a):
    import my392 as G
    x0, tsq = a
    m, S = G.world((700, 296), 8, xraw=int(round(x0 * U)))
    seq = [(tsq - 1, G.LEFT), (1, G.LEFT | G.SQUARE), (5, 0)]
    rows = G.run(m, seq, S)
    return x0, tsq, any(r['hero']['carried2C'] == G.K.ent(S[5]) for r in rows), rows[tsq - 1]['hero']['anim_t']


if __name__ == '__main__':
    mp.freeze_support()
    xs = [700.0 + k / 32 for k in range(int(2.4375 * 32) + 1)]
    with mp.Pool(22) as p:
        A = p.map(approach, xs)
        jobs = []
        for x0, flush, c, pre in A:
            jobs += [(x0, flush), (x0, flush + 1)]
        L = p.map(lift_at, jobs)
    lift = {(x0, t): (ok, an) for x0, t, ok, an in L}
    out = ['x0 ; first tick flush (PosX = 657.0) ; first tick contact == rec5 ; Square on flush tick -> lifted?(anim) ; Square on flush+1 -> lifted?(anim)']
    same = later = earlier = 0
    for x0, flush, c, pre in A:
        if c == flush:
            same += 1
        elif c > flush:
            later += 1
        else:
            earlier += 1
        out.append('  %.5f ; %s ; %s ; %s ; %s ; %s' % (x0, flush, c, lift[(x0, flush)], lift[(x0, flush + 1)], pre))
    out.append('summary: contact first set on the flush tick %d, later %d, earlier %d (of %d)' % (same, later, earlier, len(A)))
    out.append('Square on the flush tick lifts: %d of %d ; Square one tick later lifts: %d of %d' % (
        sum(1 for x0, f, c, pre in A if lift[(x0, f)][0]), len(A), sum(1 for x0, f, c, pre in A if lift[(x0, f + 1)][0]), len(A)))
    txt = '\n'.join(out) + '\n'
    open(os.path.join(os.path.dirname(os.path.abspath(__file__)), 'out', 'w4_contact.txt'), 'w').write(txt)
    print(txt)
