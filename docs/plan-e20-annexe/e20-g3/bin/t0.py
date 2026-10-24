import sys, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import time, lib392 as L
t0=time.time()
m, S = L.world392(hero_px=(560, 296))
print('world', time.time()-t0, S)
for r,s in S.items():
    e=K=L.K.ent(s)
    print('rec',r,'slot',s,'px %.3f py %.3f pz %.3f'%(m.peeks(e+0x114)/65536,m.peeks(e+0x118)/65536,m.peeks(e+0x11C)/65536),
          'box off',[m.peeks(e+o)/65536 for o in (0x1E4,0x1E8,0x1EC)],'size',[m.peeks(e+o)/65536 for o in (0x1F0,0x1F4,0x1F8)],'flags %X'%m.peek(e+0x6C),'status',m.peek(e+0x10),'terr %.2f'%(m.peeks(e+0x138)/65536))
h=L.K.ent(0)
print('hero px %.3f py %.3f pz %.3f'%(m.peeks(h+0x114)/65536,m.peeks(h+0x118)/65536,m.peeks(h+0x11C)/65536),'box off',[m.peeks(h+o)/65536 for o in (0x1E4,0x1E8,0x1EC)],'size',[m.peeks(h+o)/65536 for o in (0x1F0,0x1F4,0x1F8)],'flags %X'%m.peek(h+0x6C))
