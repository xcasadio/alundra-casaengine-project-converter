import sys, bisect
sys.path.insert(0,'.')
from lib import *
N=0xAA800//4
W=[u32(BASE+4*i) for i in range(N)]
FUNCS=[BASE+4*i for i,x in enumerate(W) if (x>>16)==0x27BD and (x&0x8000) and (x&0xFFFF)!=0]
def fo(a): return FUNCS[bisect.bisect_right(FUNCS,a)-1]
hits=[]
for i,x in enumerate(W):
    op=x>>26
    if op in (0x20,0x24,0x28,0x21,0x25,0x29,0x23,0x2B) and (x&0xFFFF)==0xBA3E:
        rs=(x>>21)&31
        # find lui rs,0x801F in previous 12 words
        for k in range(1,14):
            y=W[i-k]
            if (y>>26)==0x0F and ((y>>16)&31)==rs and (y&0xFFFF)==0x801F:
                hits.append((BASE+4*i,op,fo(BASE+4*i))); break
names={0x20:'lb',0x24:'lbu',0x28:'sb',0x21:'lh',0x25:'lhu',0x29:'sh',0x23:'lw',0x2B:'sw'}
for a,op,f in hits: print(hex(a),names[op],hex(f))
