import os, sys
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(HERE, 'oracle_h3bin'))
sys.path.insert(0, r'C:\Users\casad\AppData\Roaming\Python\Python314\site-packages')
import h3_game as G
m, STATS = G.make(0x23, hero_px=(200, 200))
p25 = G.spawn_global(m, 25, 230, 230)     # collidable (flags 0x6080)
p16 = G.spawn_global(m, 16, 260, 230)     # NOT collidable (flags 0x132144)
r = G.Run(m, lambda t: (G.CIRCLE if t == 4 else 0)); r.step(20)
n = m.peek(0x80127D2C)
lst = [m.peek(0x80127C28 + 4*i) for i in range(n)]
print("list B count", n, [hex(x) for x in lst])
print("hero %x sprite25 %x (collidable) sprite16 %x (not collidable) bomb in B: %s" % (G.HERO, p25, p16, any(m.peeks(x+0x68) == 2 for x in lst)))
na = m.peek(0x8013435C)
print("list A count", na)
