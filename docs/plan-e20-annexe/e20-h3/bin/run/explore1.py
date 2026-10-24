import sys
import spellworld as S
import world as W
from machine import s32, M32

ITEM = int(sys.argv[1], 0) if len(sys.argv) > 1 else 0x23
NT = int(sys.argv[2]) if len(sys.argv) > 2 else 80
SQ_AT = int(sys.argv[3]) if len(sys.argv) > 3 else 0
m, STATS, placed = S.make(item=ITEM, mp=4, mpmax=4, indices=(1, 2, 16, 17, 19, 20, 21, 22, 23, 24, 25))
pad = W.Pad()
H = W.ent(0)
def row(t):
    out = ['t%03d' % t]
    out.append('H anim=%#x/%#x dir=%d 2C=%#x off=(%d,%d,%d) use=%#x pos=(%.2f,%.2f,%.2f)' % (m.peeks(H+0x88), m.peeks(H+0x90), m.peeks(H+0x8C), m.peek(H+0x2C), m.peeks(H+0x30)>>16, m.peeks(H+0x34)>>16, m.peeks(H+0x38)>>16, m.peeks(0x80127164), m.peeks(H+0x114)/65536, m.peeks(H+0x118)/65536, m.peeks(H+0x11C)/65536))
    for i in range(1, 64):
        e = W.ent(i)
        st = m.peeks(e+0x10)
        if st:
            out.append('E%d st=%d ty=%#x fl=%#x t24=%d c28=%#x anim=%d/%d dir=%d aend=%d b274=%d b278=%d pos=(%.2f,%.2f,%.2f) F=(%d,%d,%d) gr=%d adj=%d hit=%d' % (i, st, m.peeks(e+0x68), m.peek(e+0x6C), m.peeks(e+0x24), m.peek(e+0x28), m.peeks(e+0x88), m.peeks(e+0x90), m.peeks(e+0x8C), m.peeks(e+0xAC), m.peeks(e+0x274), m.peeks(e+0x278), m.peeks(e+0x114)/65536, m.peeks(e+0x118)/65536, m.peeks(e+0x11C)/65536, m.peeks(e+0xC4)>>8, m.peeks(e+0xC8)>>8, m.peeks(e+0xB8)>>8, m.peeks(e+0x144), m.peeks(e+0x13C), m.peeks(e+0x220)))
    return ' | '.join(out)
prev = None
for t in range(1, NT + 1):
    hold = 0
    if t == 1: hold |= W.CIRCLE
    if SQ_AT and t == SQ_AT: hold |= W.SQUARE
    m.tick_no = t
    j0 = len(m.journal)
    W.tick(m, pad, hold, check_pad=False)
    r = row(t)
    js = [j for j in m.journal[j0:] if not j[1].startswith('pass_')]
    print(r + (' || J ' + str(js) if js else ''))
