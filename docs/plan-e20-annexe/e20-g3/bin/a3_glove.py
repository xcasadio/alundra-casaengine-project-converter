import sys, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import lib392 as L
m, S = L.world392(hero_px=(720, 296))
print('GetNumberOfItem(0x3B) in the new-game kit =', m.call(0x8004E428, 0x3B))
for r, s in S.items():
    f = m.peek(L.K.ent(s) + 0x6C)
    print('rec%d flags=%X kind=(flags&0x600)>>9=%d' % (r, f, (f & 0x600) >> 9))
