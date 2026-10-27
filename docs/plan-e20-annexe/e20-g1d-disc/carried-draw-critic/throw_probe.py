"""READ-ONLY probe (copy of the h3 oracle ./oracle_h3bin, REAL code of ALUN_CD.EXE France): the bomb carried standing, then THROWN with the Square (edge at tick 50), hero facing each of the 4 directions.
Per tick after the release: the offset of the bomb to the hero (px), the bomb Z, the keys (REAL sort driver 0x800399B8 run after each tick), who is drawn first.
Question: for how many ticks does the thrown bomb stay in front / behind, and how long does it overlap the hero's body box?"""
import os, sys
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(HERE, 'oracle_h3bin'))
sys.path.insert(0, r'C:\Users\casad\AppData\Roaming\Python\Python314\site-packages')
import h3_game as G
import world as W

def case(face):
    m, STATS = G.make(0x23, hero_px=(200, 200))
    holdface = G.HOLD[face]
    def holds(t):
        h = 0
        if t <= 3: h |= holdface        # turn to the wanted direction (the walk makes the hero face it; 3 ticks)
        if t == 4: h |= G.CIRCLE
        if t == 50: h |= G.SQUARE
        return h
    r = G.Run(m, holds)
    rows = []
    def post(m, t):
        W.run_pass(m, 0x800399B8)
        bomb = None
        for i in range(1, 40):
            q = W.ent(i)
            if m.peeks(q + 0x10) in (2, 3) and m.peeks(q + 0x68) == 2:
                bomb = q; break
        if bomb is None: return
        H = G.HERO
        rows.append((t, m.peek(bomb + 0x28) != 0, (m.peeks(bomb + 0x114) - m.peeks(H + 0x114)) / 65536, (m.peeks(bomb + 0x118) - m.peeks(H + 0x118)) / 65536, m.peeks(bomb + 0x11C) / 65536,
                     m.peek(H + 0x1BC) & 0xFFFFFFFF, m.peek(bomb + 0x1BC) & 0xFFFFFFFF, m.peeks(bomb + 0x1A8), m.peeks(H + 0x1A8), m.peeks(bomb + 0x90)))
    r.step(90, post=post)
    print("== facing %s" % face)
    first_free = None
    prev = None
    for t, carried, dx, dy, z, hk, bk, bi, hi, banim in rows:
        if t < 48: continue
        v = 'FRONT' if bk > hk else ('BEHIND' if bk < hk else 'EQ')
        if (carried, v) != prev or t < 56:
            print("   t=%2d carried=%d bomb offset (%+6.1f,%+6.1f) z=%5.1f hero key %08X (idsv %d) bomb key %08X (idsv %d anim %d) -> %s" % (t, carried, dx, dy, z, hk, hi, bk, bi, banim, v))
        prev = (carried, v)
for face in ('right', 'left', 'down', 'up'):
    case(face)
