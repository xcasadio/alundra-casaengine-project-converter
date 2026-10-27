import spellworld as S, world as W
P = W.ent(0)
m, STATS, placed = S.make(item=0x2C, mp=4, mpmax=4, indices=(1, 16, 17, 19, 20, 21, 22, 23, 24))
print('hero +0x8C (target dir) at make:', m.peeks(P+0x8C), ' +0x94 cur dir', m.peeks(P+0x94), ' +0x98', m.peeks(P+0x98), 'x', m.peeks(P+0x114)>>16, 'y', m.peeks(P+0x118)>>16)
pad = W.Pad()
for t in range(1, 70):
    m.tick_no = t
    W.tick(m, pad, W.CIRCLE if t == 1 else 0, check_pad=False)
    if t in (1,2,10,59):
        print('t',t,'dir',m.peeks(P+0x8C), m.peeks(P+0x94), 'anim', m.peeks(P+0x88))
