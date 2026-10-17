"""w7_return.py - critic: which model gives the XY move's return value (the contact +0x130)?  My reading of 0x80037730:
  the return is 0x34(sp), written only at the loop head 0x800377CC with $s4, zeroed at 0x80037DC8 (no force), and $s4 = 0 again at 0x800377B4
  (start and slide re-entry).  $s4 is set at 0x80037910 from the test at 0x80037908, but the ground step-up branch (0x8003783C-0x800378CC) that finds
  no candidate jumps to the terrain test at 0x8003791C WITHOUT writing $s4.  Two models of the X-only halving loop (hero pushing west on flat ground):
    STICKY    : $s4 is left unchanged by an accepted step-up attempt (my reading);
    PLAIN     : $s4 = result of every attempt (the 'second-to-last attempt' wording of the discovery).
Real run: start (x0, 296), hold LEFT; at the tick the hero becomes flush (PosX = 657.0), compare the contact with both models, fed with the real
pre-tick PosX and the real step (pre-tick PosX - would-be PosX is not observable, so the step is read from the hero's +0xE4 force word... not
written back: +0xE4 is the per-tick move, read BEFORE the tick from the machine).  x0 = 700 + k * 977 units (odd low bits).  Usage: python -B -I w7_return.py"""
import os
import sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import multiprocessing as mp

U = 65536
FACE = 647 * U          # rec5's east face; the hero box min X = PosX - 10 px


def model(p, s, sticky):
    s4 = 0
    s5 = 0
    pos = p
    while True:
        head = s4
        trial = pos + s
        overlap = (trial - 10 * U) < FACE
        if overlap:
            s4 = 1
        elif not sticky:
            s4 = 0
        if not overlap:
            pos = trial
            if s5 == 0:
                return head, pos
        s = 0 if s == -1 else (s >> 1)
        if s == 0:
            return head, pos
        s5 += 1


def one(k):
    import my392 as G
    W = G.gl.W
    x0 = 700 * U + k * 977
    m, S = G.world((700, 296), 8, xraw=x0)
    h = G.K.ent(0)
    s5 = G.K.ent(S[5])
    pad = W.Pad()
    out = None
    for t in range(1, 40):
        p = m.peeks(h + 0x114)
        m.tick_no = t
        W.tick(m, pad, G.LEFT)
        q = m.peeks(h + 0x114)
        if q == 657 * U and p != q:
            s = m.peeks(h + 0xE4)        # the force word the XY move used this tick (+0xE4 is not rewritten by the move except by a slide)
            got = 1 if m.peek(h + 0x130) == s5 else (0 if m.peek(h + 0x130) == 0 else 2)
            st, pos1 = model(p, s, True)
            pl, pos2 = model(p, s, False)
            out = (k, t, p, s, got, st, pl, pos1 == q, pos2 == q)
            break
    return out


if __name__ == '__main__':
    mp.freeze_support()
    with mp.Pool(22) as pool:
        R = [r for r in pool.map(one, range(0, 160)) if r]
    agree_st = sum(1 for r in R if r[4] == r[5])
    agree_pl = sum(1 for r in R if r[4] == r[6])
    lines = ['k, flush tick, pre PosX (raw), step +0xE4, measured contact (1 rec5, 0 none), STICKY, PLAIN, model position ok (sticky, plain)']
    for r in R:
        if r[5] != r[6] or r[4] != r[5]:
            lines.append('  %s' % (r,))
    lines.append('runs %d ; measured == STICKY: %d ; measured == PLAIN: %d ; runs where the two models differ: %d' % (
        len(R), agree_st, agree_pl, sum(1 for r in R if r[5] != r[6])))
    txt = '\n'.join(lines) + '\n'
    open(os.path.join(os.path.dirname(os.path.abspath(__file__)), 'out', 'w7_return.txt'), 'w').write(txt)
    print(txt)
