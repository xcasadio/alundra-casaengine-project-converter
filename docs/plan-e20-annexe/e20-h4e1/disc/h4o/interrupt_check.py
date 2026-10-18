import spellworld as S, world as W
from machine import s32
P = W.ent(0)
for item, t_hit in ((0x2C, 100), (0x2D, 100), (0x2F, 60), (0x30, 60), (0x32, 60)):
    m, STATS, placed = S.make(item=item, mp=4, mpmax=4, indices=(1, 16, 17, 19, 20, 21, 22, 23, 24))
    pad = W.Pad()
    before = None
    for t in range(1, t_hit + 40):
        if t == t_hit:
            m.poke(P + 0x88, 0x31)                      # the hurt state set by the damage entry (0x80031C84)
            snap = [(i, m.peeks(S.ent(i) + 0x10), m.peeks(S.ent(i) + 0x68)) for i in range(1, 64) if m.peeks(S.ent(i) + 0x10)]
            before = snap
        m.tick_no = t
        W.tick(m, pad, W.CIRCLE if t == 1 else 0, check_pad=False)
        if t in (t_hit, t_hit + 1, t_hit + 2):
            sts = [(i, m.peeks(S.ent(i) + 0x10), m.peeks(S.ent(i) + 0x68), m.peeks(S.ent(i) + 0x88)) for i in range(1, 64) if m.peeks(S.ent(i) + 0x10)]
            print('item %#x tick %d: item_in_use %#x hero anim %#x  entities (slot,status,type,tanim): %s' % (item, t, m.peeks(0x80127164), m.peeks(P + 0x88), sts[:8]))
    fx = [(j[0], j[2][1], j[2][2]) for j in m.journal if j[1] in ('effect_3BDD8', 'effect_3BE74')]
    print('   effects after: %s' % fx[-12:])
