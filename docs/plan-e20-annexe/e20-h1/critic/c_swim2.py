import os, sys
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(HERE, 'h4o'))
import c_swim as C
import world as W
def go(water, label, pad_fn):
    m, STATS, placed = C.S.make(item=0x24, mp=4, mpmax=4, indices=(1, 2), extra_items=[0x1B])
    if water:
        C.make_water(m)
    m.poke(C.HERO + 0x14, 3); m.poke(C.HERO + 0x18, 10)
    pad = W.Pad()
    for t in range(1, 25):
        m.journal.clear(); m.tick_no = t
        W.tick(m, pad, pad_fn(t), check_pad=False)
        ev = [(j[1], j[2]) for j in m.journal if j[1] in ('sound', 'effect_3BDD8')]
        if any(e[0] == 'effect_3BDD8' for e in ev):
            print(label, 'tick', t, 'pos', (m.peeks(C.HERO + 0x114), m.peeks(C.HERO + 0x118), m.peeks(C.HERO + 0x11C)), 'anim %02X' % (m.peeks(C.HERO + 0x88) & 0xFF), 'events', ev, 'hp', m.peeks(C.HERO + 0x14))
go(False, 'dry  ', lambda t: W.CIRCLE if t == 10 else 0)
go(True, 'water', lambda t: W.CIRCLE if t == 10 else 0)
go(True, 'swim ', lambda t: (W.RIGHT | W.CIRCLE) if t == 20 else W.RIGHT)
