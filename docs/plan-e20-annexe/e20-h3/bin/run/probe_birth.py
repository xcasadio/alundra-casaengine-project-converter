import h3_game as G
from h3_game import *
for item, start in ((0x1F, 0x80034224), (0x23, 0x8003453C)):
    m, STATS = G.make(item, hero_px=(200, 200))
    # the real start function alone, on a fresh machine, hero idle on the ground: nothing else runs
    m.poke(HERO + 0x144, 1)
    rng0 = m.peek(0x80098708)
    r = m.call(start)
    rng1 = m.peek(0x80098708)
    e = W.ent(1)
    print('item %#x start returned %d; RNG %#x -> %#x (%s)' % (item, r, rng0, rng1, 'unchanged' if rng0 == rng1 else 'CHANGED'))
    print('  hero: 2C=%#x anim(+0x88)=%#x offsets=(%d,%d,%d) use=%#x' % (m.peek(HERO + 0x2C), m.peeks(HERO + 0x88), m.peeks(HERO + 0x30), m.peeks(HERO + 0x34), m.peeks(HERO + 0x38), m.peeks(0x80127164)))
    print('  born: slot1 status=%d type(+0x68)=%d flags=%#x c28=%#x fuse(+0x274)=%d dir(+0x8C)=%d pos=(%.2f,%.2f,%.4f) sheet=%#x curAtk=%#x hp=%d' % (
        m.peeks(e + 0x10), m.peeks(e + 0x68), m.peek(e + 0x6C), m.peek(e + 0x28), m.peeks(e + 0x274), m.peeks(e + 0x8C), m.peeks(e + 0x114) / 65536, m.peeks(e + 0x118) / 65536, m.peeks(e + 0x11C) / 65536, m.peek(e + 0x1C4), m.peek(e + 0x1C8), m.peeks(e + 0x14)))
    print('  records: id(+4)=%d spriteRecord(+0x64)=%#x progIdx A..F=%s sprIdx=%s contents(+0x3C)=%d' % (m.peeks(e + 4), m.peek(e + 0x64), [m.peeks(e + o) for o in (0x4C, 0x50, 0x54, 0x58, 0x5C, 0x60)], [m.peeks(e + o) for o in (0x70, 0x74, 0x78, 0x7C, 0x80, 0x84)], m.peeks(e + 0x3C)))
    print('  sounds journal:', [(j[1], j[2]) for j in m.journal if j[1] == 'sound'])
