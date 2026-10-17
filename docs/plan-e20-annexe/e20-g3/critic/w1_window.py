"""w1_window.py - critic: the Y window of map 392's crate column, measured independently on the REAL code (my392 harness, 12 crates in file order).
JUMP: start (xs, y) facing the column, hold DIR, DIR+CROSS at tick tj (8..26), DIR to tick 80.  Success = some tick with hero support +0x12C == rec5 and ground +0x144 != 0.
       Also reported: the hero ends past the column (west of x 615 from the east, east of x 657 from the west).
LIFT : start (720, y), hold LEFT; the first tick after a tick that ended with contact +0x130 == rec5, press SQUARE (LEFT held); then release until the carry
       state, then LEFT for 120 ticks.  Success = PosX <= 600 at the end (past the column).  Reported: tick of contact, the carried slot, the final X, the
       contact at the end.
Adaptive pad decided from the state read before each tick (no state write).  Usage: python -B -I w1_window.py"""
import os
import sys
import json
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import multiprocessing as mp

U = 65536
YS = [294 * U + U // 2, 295 * U - 1, 295 * U, 295 * U + 1, 296 * U, 297 * U - 1, 297 * U, 297 * U + 1, 297 * U + U // 2]


def jump(a):
    import my392 as G
    side, yraw, tj = a
    DIR = G.LEFT if side == 'east' else G.RIGHT
    xs = 720 if side == 'east' else 560
    m, S = G.world((xs, 296), 8 if side == 'east' else 24, yraw=yraw)
    s5 = G.K.ent(S[5])
    rows = G.run(m, [(tj - 1, DIR), (1, DIR | G.CROSS), (80 - tj, DIR)], S)
    on5 = [r['tick'] for r in rows if r['hero']['support'] == s5 and r['hero']['ground']]
    fx = rows[-1]['hero']['px'] / U
    past = fx < 615 if side == 'east' else fx > 657
    ymin = min(r['hero']['py'] for r in rows)
    ymax = max(r['hero']['py'] for r in rows)
    return dict(side=side, y=yraw, tj=tj, on5=on5[:1], final_x=fx, past=past, ymin=ymin, ymax=ymax)


def lift(yraw):
    import my392 as G
    W = G.gl.W
    m, S = G.world((720, 296), 8, yraw=yraw)
    K = G.K
    h = K.ent(0)
    slot_of = {K.ent(s): r for r, s in S.items()}
    pad = W.Pad()
    phase, t_contact, t_sq, carried, log = 'push', None, None, None, []
    for t in range(1, 260):
        xc = m.peek(h + 0x130)
        if phase == 'push':
            hold = G.LEFT
            if xc in slot_of:
                t_contact = t - 1
                hold = G.LEFT | G.SQUARE
                t_sq = t
                phase = 'lifted?'
        elif phase == 'lifted?':
            carried = slot_of.get(m.peek(h + 0x2C))
            hold = 0
            phase = 'wait'
        elif phase == 'wait':
            hold = 0
            if m.peeks(h + 0x88) in (0x0C, 0x30) or t - t_sq > 60:
                phase = 'carry'
        else:
            hold = G.LEFT
        m.tick_no = t
        W.tick(m, pad, hold)
        log.append((t, hold, m.peeks(h + 0x114) / U, m.peeks(h + 0x118) / U, slot_of.get(m.peek(h + 0x130)), slot_of.get(m.peek(h + 0x2C)), m.peeks(h + 0x88)))
    fx = m.peeks(h + 0x114) / U
    return dict(y=yraw, t_contact=t_contact, contact_rec=log[t_contact - 1][4] if t_contact else None,
                carried=carried, final_x=fx, final_y=m.peeks(h + 0x118) / U, past=fx <= 600, end_contact=slot_of.get(m.peek(h + 0x130)),
                anim_after_sq=[x[6] for x in log[t_sq - 1:t_sq + 3]] if t_sq else None)


if __name__ == '__main__':
    mp.freeze_support()
    jobs = [(side, y, tj) for side in ('east', 'west') for y in YS for tj in range(8, 27)]
    with mp.Pool(22) as p:
        J = p.map(jump, jobs, chunksize=4)
        L = p.map(lift, YS)
    out = []
    from collections import defaultdict
    d = defaultdict(list)
    for r in J:
        d[(r['side'], r['y'])].append(r)
    out.append('JUMP: side, PosY (raw / px), landed on rec5 for k of 19 Cross ticks [ticks], ended past the column for k of 19, PosY range seen')
    for (side, y) in sorted(d, key=lambda k: (k[0], k[1])):
        rr = d[(side, y)]
        ok = [r['tj'] for r in rr if r['on5']]
        past = [r['tj'] for r in rr if r['past']]
        out.append('  %-4s y=%d (%.7f): on rec5 %2d/19 %s ; past %2d/19 ; PosY seen [%.7f, %.7f]' % (
            side, y, y / U, len(ok), ok, len(past), min(r['ymin'] for r in rr) / U, max(r['ymax'] for r in rr) / U))
    out.append('LIFT from the east: PosY, contact tick, carried record, final X, final Y, past, contact at the end')
    for r in L:
        out.append('  y=%d (%.7f): contact at end of tick %s, carried rec %s, final x %.4f y %.7f, past=%s, end contact rec %s, anim after Square %s' % (
            r['y'], r['y'] / U, r['t_contact'], r['carried'], r['final_x'], r['final_y'], r['past'], r['end_contact'], r['anim_after_sq']))
    txt = '\n'.join(out) + '\n'
    open(os.path.join(os.path.dirname(os.path.abspath(__file__)), 'out', 'w1_window.txt'), 'w').write(txt)
    print(txt)
