import sys, bisect
sys.path.insert(0,'.')
from lib import *
N=0xAA800//4
W=[u32(BASE+4*i) for i in range(N)]
FUNCS=[BASE+4*i for i,x in enumerate(W) if (x>>16)==0x27BD and (x&0x8000) and (x&0xFFFF)!=0]
def fo(a): return FUNCS[bisect.bisect_right(FUNCS,a)-1]
t=0x800315B0
print([ (hex(BASE+4*i),hex(fo(BASE+4*i))) for i,x in enumerate(W) if (x>>26)==3 and (((x&0x03FFFFFF)<<2)|0x80000000)==t])
print('jr/jump refs', [hex(BASE+4*i) for i,x in enumerate(W) if x==t])
print('pointer at 0x80029BC4 value', hex(u32(0x80029BC4)))
