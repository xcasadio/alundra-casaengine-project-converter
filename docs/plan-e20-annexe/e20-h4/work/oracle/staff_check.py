import spellworld as S, world as W
from machine import s32
for label, kw in (("staff equipped (weapon slot 6, item 7 owned), MP 0", dict(mp=0, weapon=6, extra_items=(7,))),
                  ("sword equipped, MP 0", dict(mp=0, weapon=1)),
                  ("sword equipped, MP 2", dict(mp=2, weapon=1)),
                  ("staff equipped, MP 2", dict(mp=2, weapon=6, extra_items=(7,)))):
    m, STATS, placed = S.make(item=0x2B, mpmax=4, indices=(1, 16), **kw)
    w = s32(m.call(0x8004E030))
    mp0 = m.peek(STATS + 4) & 0xFFFF
    pad = W.Pad()
    started = False
    for t in range(1, 6):
        m.tick_no = t
        W.tick(m, pad, W.CIRCLE if t == 1 else 0, check_pad=False)
        if m.peeks(0x80127164):
            started = True
    sounds = [j[2] for j in m.journal if j[1] == 'sound' and j[2] != 0]
    print('%-55s weapon id %d  MP %d -> %d  cast started %s  hero anim %#x  sounds %s' % (label, w, mp0, m.peek(STATS + 4) & 0xFFFF, started, m.peeks(W.ent(0) + 0x88), sounds))
