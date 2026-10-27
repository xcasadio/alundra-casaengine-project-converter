import os, sys
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(HERE, 'oracle_h3bin'))
sys.path.insert(0, r'C:\Users\casad\AppData\Roaming\Python\Python314\site-packages')
import h3_game as G
import world as W
m, STATS = G.make(0x23, hero_px=(200, 200))
p = G.spawn_global(m, 1, 200, 208)
r = G.Run(m, lambda t: (G.CIRCLE if t == 4 else 0))
def post(m, t):
    if t in (1,2,3,4,5,6,8,10,14,20,30):
        W.run_pass(m, 0x800399B8)
        print("t=%2d st=%d fl=%08X 24=%d y=%.2f z=%.2f hp=%d 14=%d type68=%d" % (t, m.peeks(p+0x10), m.peek(p+0x6C), m.peeks(p+0x24), m.peeks(p+0x118)/65536, m.peeks(p+0x11C)/65536, m.peeks(p+0x14), m.peeks(p+0x14), m.peeks(p+0x68)))
r.step(30, post=post)
print([ (j[1:]) for j in m.journal if not j[1].startswith('pass_')][:15])
