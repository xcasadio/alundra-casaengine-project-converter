"""Critic: effect position vs hero position when the hero MOVES (walk east on land, Triangle run, jump) at the tick of the drink."""
import os, sys
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(HERE, 'h4o'))
import spellworld as S
import world as W
HERO = W.ent(0)
def go(label, pad_fn, ticks, circle_tick):
    m, STATS, placed = S.make(item=0x24, mp=4, mpmax=4, indices=(1, 2), extra_items=[0x1B])
    m.poke(HERO + 0x14, 3); m.poke(HERO + 0x18, 10)
    pad = W.Pad()
    prev = (m.peeks(HERO + 0x114), m.peeks(HERO + 0x118), m.peeks(HERO + 0x11C))
    for t in range(1, ticks + 1):
        m.journal.clear(); m.tick_no = t
        W.tick(m, pad, pad_fn(t), check_pad=False)
        post = (m.peeks(HERO + 0x114), m.peeks(HERO + 0x118), m.peeks(HERO + 0x11C))
        eff = [j[2] for j in m.journal if j[1] == 'effect_3BDD8']
        if eff:
            print(label, 'tick', t, 'hero pos before the tick', prev, 'after', post, 'effect (x,y,z)', eff[0][3:6], 'anim %02X' % (m.peeks(HERO + 0x88) & 0xFF), 'hp', m.peeks(HERO + 0x14))
        prev = post
go('walk east   ', lambda t: (W.RIGHT | W.CIRCLE) if t == 30 else W.RIGHT, 40, 30)
go('run (Tri+R) ', lambda t: (W.TRIANGLE | W.RIGHT | W.CIRCLE) if t == 40 else (W.TRIANGLE | W.RIGHT), 50, 40)
go('jump (Cross)', lambda t: (W.CROSS | W.CIRCLE) if t == 6 else (W.CROSS if t == 1 else 0), 12, 6)
