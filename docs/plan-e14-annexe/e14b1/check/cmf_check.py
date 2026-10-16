import mymips, hh
from hh import Ent, M32, s32
import models_copy as M
m = mymips.Mach()
ENT0=0x80127D30
def real_cmf(floor, mh, td, dist):
    e=Ent(); e.set32(M.FLOOR,floor); e.set32(M.TDIR,td)
    for k in range(4): e.set32(M.MH[k], mh[k])
    a=ENT0+5*0x294
    for i,b in enumerate(e.raw): m.wb(a+i,b)
    return m.call(0x80080260, a, dist) & M32
def decomp(floor, mh, td, dist):
    adj=dist+1
    f0,f1,f2,f3=[s32(floor-x) for x in mh]
    u=lambda x:x&M32
    cs=True
    if adj<f0:
        cs=False
        if adj<f1:
            if u(td-9)<0xf: return 1
            cs=True
    if cs and adj<f1:
        if f3<=adj: return skip(f0,f2,adj,td)
        if u(td-0x11)<0xf: return 1
    if adj<f3:
        if f2<=adj: return 0
        if 0x10<u(td-8): return 1
    return skip(f0,f2,adj,td)
def skip(f0,f2,adj,td):
    r=0
    if f2<=adj or adj<f0:
        r=1
        if 0xf<td: r=0
    return r
def mine(floor,mh,td,dist):
    # my own reading of the binary: b_k = (dist+1) < floor - mh[k]
    thr=dist+1
    b=[thr<s32(floor-x) for x in mh]
    u=lambda x:x&M32
    return int((b[0] and b[1] and u(td-9)<15) or (b[1] and b[3] and u(td-17)<15) or (b[3] and b[2] and not (u(td-8)<17)) or (b[2] and b[0] and td<16))
tot=diffd=diffr=0
# own enumeration: for each corner pattern, 32 directions, dists 0,1,0x100000, plus corner depth variants
import itertools
for pat in range(16):
    for td in range(32):
        for dist in (0,1,0x100000,5):
            floor=0x300000
            for deep in (6, 0x200000):
                for shallow in (-1, 0, 1):
                    thr=dist+1
                    mh=[floor-(thr+deep) if (pat>>k)&1 else floor-max(thr+shallow-1,-3) for k in range(4)]
                    r=real_cmf(floor,mh,td,dist); d=decomp(floor,mh,td,dist); mi=mine(floor,mh,td,dist)
                    tot+=1
                    if r!=mi: diffr+=1
                    if r!=d: diffd+=1
print('cases',tot,'real!=mine',diffr,'real!=decomp',diffd)
# the report's enumeration (16 x 32 x 3 dists)
n=0;dd=0
for pat in range(16):
    for td in range(32):
        for dist in (0,1,0x100000):
            floor=0x300000; thr=dist+1
            mh=[floor-(thr+6) if (pat>>k)&1 else floor-max(thr-1,0) for k in range(4)]
            n+=1
            if real_cmf(floor,mh,td,dist)!=decomp(floor,mh,td,dist): dd+=1
print('report enumeration', n, 'real!=decomp', dd)
# flat ground, TD<16
print('flat TD=0..31 dist 0: real', [real_cmf(0x300000,[0x300000]*4,t,0) for t in range(32)])
print('flat TD=0..31 dist 0: decomp', [decomp(0x300000,[0x300000]*4,t,0) for t in range(32)])
