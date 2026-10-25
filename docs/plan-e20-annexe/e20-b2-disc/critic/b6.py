import sys, bisect
sys.path.insert(0,'.')
from lib import *
N=0xAA800//4
W=[u32(BASE+4*i) for i in range(N)]
FUNCS=[BASE+4*i for i,x in enumerate(W) if (x>>16)==0x27BD and (x&0x8000) and (x&0xFFFF)!=0]
def fo(a): return FUNCS[bisect.bisect_right(FUNCS,a)-1]
names={0x20:'lb',0x24:'lbu',0x28:'sb'}
for i,x in enumerate(W):
    if (x>>26) in names and (x&0xFFFF)==0x756:
        a=BASE+4*i
        print(hex(a),names[x>>26],hex(fo(a)))
