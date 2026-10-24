import h3w
from h3w import *
m, STATS, placed = S.make(item=0x23, mp=4, mpmax=4, indices=(1, 2, 25))
pad = W.Pad()
m.tick_no = 1
W.tick(m, pad, W.CIRCLE, check_pad=False)
for i in (1,):
    e = W.ent(i)
    sh = m.peek(e + 0x1C4)
    print('slot', i, 'type', m.peeks(e + 0x68), 'sheet ptr %#x' % sh, 'hp', m.peeks(e+0x14), 'hpmax', m.peeks(e+0x18))
    raw = bytes(m.ram[sh & 0x1FFFFFFF:(sh & 0x1FFFFFFF) + 0x40])
    print(raw.hex(' '))
    n = raw[0xE]
    print('AttackCount byte +0xE =', n)
    # attacks list: structure is read by the hit pass through +0x1C8 (current attack pointer)
    print('responses +2..+0x10:', list(raw[2:0x10]))
    print('cur attack ptr %#x' % m.peek(e+0x1C8), 'bytes', list(m.ram[(m.peek(e+0x1C8)&0x1FFFFFFF):(m.peek(e+0x1C8)&0x1FFFFFFF)+4]) if m.peek(e+0x1C8) else None)
    print('flags %#x' % m.peek(e+0x6C), 'size', m.peeks(e+0x1DC), 'pos', m.peeks(e+0x114)/65536)
    for off in (0x1B4,0x1B8,0x1BC,0x1C0,0x1C4,0x1C8,0x1CC,0x1D0,0x1D4,0x1D8,0x1DC,0x1E0,0x1E4,0x1E8,0x1EC):
        print(hex(off), hex(m.peek(e+off)), end='; ')
    print()
