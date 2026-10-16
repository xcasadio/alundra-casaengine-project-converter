import random
import mymips
from hh import Ent, M32, s32
import models_copy as M
m = mymips.Mach()
ENT0=0x80127D30
def real(floor, mh, td, dist):
    e=Ent(); e.set32(M.FLOOR,floor); e.set32(M.TDIR,td)
    for k in range(4): e.set32(M.MH[k], mh[k])
    a=ENT0+5*0x294
    for i,b in enumerate(e.raw): m.wb(a+i,b)
    return m.call(0x80080260, a, dist) & M32
def InRange(d,lo,count): return ((d-lo)&M32) < count
def dll(floor, mh, direction, dist):
    thr=dist+1
    t0,t1,t2,t3=[s32(floor-x) for x in mh]
    a=thr<t0; b=thr<t1; d=thr<t2; c=thr<t3
    state=0x19C
    if a:
        if b:
            if InRange(direction,9,15): return True
        else:
            state=0x1CC
    if state==0x19C:
        if not b: state=0x1CC
        elif not c: state=0x1F8
        else:
            if InRange(direction,0x11,15): return True
            state=0x1CC
    if state==0x1CC:
        if c:
            if not d: return False
            if not InRange(direction,8,0x11): return True
    if (not d) or (not a): return False
    return direction<0x10
rnd=random.Random(5)
bad=0;N=60000;ex=[]
for _ in range(N):
    floor=rnd.choice([0,0x100000,0x300000,-0x100000])
    dist=rnd.choice([0,1,2,0x100000,-1])
    thr=dist+1
    mh=[floor-(thr+rnd.choice([-3,-1,0,1,2,5,0x100000])) for _ in range(4)]
    td=rnd.randrange(32)
    r=real(floor,mh,td,dist); d=int(dll(floor,mh,td,dist))
    if r!=d:
        bad+=1
        if len(ex)<3: ex.append((floor,mh,td,dist,r,d))
print('DLL tree vs real CanMoveForward:',N,'cases,',bad,'mismatches',ex)
