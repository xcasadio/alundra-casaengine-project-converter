"""READ-ONLY probe on a COPY of the h3 oracle (./oracle_h3bin): what are the lists the sort driver F (0x800397AC) reads?
List A = 0x80134250 (count 0x8013435C): the visible entities the draw list is built from. List B = 0x80127C28 (count 0x80127D2C): the list of the overlap loop 0x80039860-0x80039978."""
import os, sys
HERE = os.path.dirname(os.path.abspath(__file__))
O = os.path.join(HERE, 'oracle_h3bin')
sys.path.insert(0, O)
sys.path.insert(0, r'C:\Users\casad\AppData\Roaming\Python\Python314\site-packages')
import h3_game as G
import world as W

m, STATS = G.make(0x23, hero_px=(200, 200))
def holds(t):
    h = 0
    if t <= 3: h |= G.RIGHT
    if t == 4: h |= G.CIRCLE
    return h
r = G.Run(m, holds)
r.step(60)

def dump(base, cnt_addr, name):
    n = m.peek(cnt_addr)
    print(name, 'count', n)
    for i in range(min(n, 64)):
        p = m.peek(base + 4*i)
        print("   [%2d] %08X  st=%d type68=%d flags6C=%08X afl=%02X car28=%08X idsv=%d y=%.2f z=%.2f 1C0=%d 1E0=%d 1F8=%d" % (
            i, p, m.peeks(p+0x10), m.peeks(p+0x68), m.peek(p+0x6C), m.peek(p+0xB4) & 0xFF, m.peek(p+0x28), m.peeks(p+0x1A8), m.peeks(p+0x118)/65536, m.peeks(p+0x11C)/65536, m.peeks(p+0x1C0), m.peeks(p+0x1E0), m.peeks(p+0x1F8)))
dump(0x80134250, 0x8013435C, 'list A (draw list source)')
dump(0x80127C28, 0x80127D2C, 'list B (overlap loop)')
print('hero', hex(G.HERO))
