import sys, struct
sys.path.insert(0,'.')
from lib import *
N=(0xAA800)//4
W=[u32(BASE+4*i) for i in range(N)]
def callers(t):
    out=[]
    for i,x in enumerate(W):
        if (x>>26)==3 and (((x&0x03FFFFFF)<<2)|0x80000000)==t: out.append(BASE+4*i)
    return out
import bisect
FUNCS=[BASE+4*i for i,x in enumerate(W) if (x>>16)==0x27BD and (x&0x8000) and (x&0xFFFF)!=0]
def fo(a):
    k=bisect.bisect_right(FUNCS,a)-1
    return FUNCS[k]
for t in (0x8004E030,0x8004E18C):
    cs=callers(t)
    print(hex(t),[ (hex(c),hex(fo(c))) for c in cs])
# wrappers 0x8004E268..0x8004E308 callers
for t in range(0x8004E268,0x8004E310,4):
    if (W[(t-BASE)>>2]>>16)==0x27BD:
        cs=callers(t)
        print('wrapper',hex(t),[ (hex(c),hex(fo(c))) for c in cs])
