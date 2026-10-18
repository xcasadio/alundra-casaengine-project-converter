"""scen1.py - critic: bomb timeline in the emulated game (Circle at tick 1). Everything computed from the real code."""
import sys
import spellworld as S
import world as W
from machine import s32, M32

def main(item=0x23, nticks=320, press=(1,)):
    m, STATS, placed = S.make(item=item)
    pad = W.Pad()
    prev = {}
    H = W.ent(0)
    print('equipped', hex(m.peek(STATS + 0xC) & 0xFFFF), 'hero hp', m.peeks(H + 0x14), 'pos', m.peeks(H + 0x114) / 65536, m.peeks(H + 0x118) / 65536)
    for t in range(1, nticks + 1):
        hold = W.CIRCLE if t in press else 0
        m.tick_no = t
        j0 = len(m.journal)
        W.tick(m, pad, hold, check_pad=False)
        row = []
        for i in range(1, 6):
            e = W.ent(i)
            st = m.peeks(e + 0x10)
            if st or prev.get(i):
                row.append((i, st, m.peeks(e + 0x68), m.peeks(e + 0x88), m.peeks(e + 0x90), m.peeks(e + 0x274), m.peeks(e + 0x11C) / 65536, m.peeks(e + 0xAC)))
        h = (m.peeks(H + 0x88), m.peeks(H + 0x90), m.peek(H + 0x2C), m.peeks(0x80127164 - 0) if False else m.peek(0x80127164))
        new = [x for x in m.journal[j0:]]
        snap = (tuple(row), h)
        if snap != prev.get('s') or new:
            print('t%3d' % t, 'hero(tgt,cur,link,item)=', (h[0], h[1], hex(h[2]), hex(h[3])), 'ents', row, 'journal', new)
        prev['s'] = snap
        for i in range(1, 6):
            prev[i] = m.peeks(W.ent(i) + 0x10)
if __name__ == '__main__':
    main(int(sys.argv[1], 16) if len(sys.argv) > 1 else 0x23, int(sys.argv[2]) if len(sys.argv) > 2 else 320)
