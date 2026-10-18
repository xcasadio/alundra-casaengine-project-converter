import struct, sys
from mydis import *
# scan for references to the blank-case template 0x80134368 .. +0x294
lo, hi = 0x80134368, 0x80134368+0x294
words = [(T_ADDR+4*i, struct.unpack_from("<I", DATA, 0x800+4*i)[0]) for i in range((len(DATA)-0x800)//4)]
res=[]
for i,(a,w) in enumerate(words):
    op=w>>26
    if op==0x0F: # lui
        rt=(w>>16)&31; imm=w&0xffff
        if imm in (0x8013,0x8014) or (imm==0x8013):
            # scan next 12 words
            for j in range(i+1,min(i+14,len(words))):
                a2,w2=words[j]
                op2=w2>>26; rs=(w2>>21)&31; imm2=w2&0xffff
                if imm2>=0x8000: imm2-=0x10000
                if op2 in (0x23,0x2b,0x24,0x25,0x20,0x21,0x28,0x29,0x09,0x0D) and rs==rt:
                    full=(imm<<16)+imm2
                    if lo<=full<hi:
                        res.append((a2,hex(full),op2))
for r in res: print(hex(r[0]),r[1],hex(r[2]))
print(len(res))
