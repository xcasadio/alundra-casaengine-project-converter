import sys
sys.path.insert(0, r'C:\Users\casad\AppData\Local\Temp\claude\D--development-repo-alundra-casaengine-project-converter\b00d1a72-420d-4acc-ab34-0c25fbdad3a1\scratchpad\t2-next\g2c3-critic')
from bx import *
lo=int(sys.argv[1],16)  # 16-bit immediate (signed handled)
hi=int(sys.argv[2],16)  # high half for lui
base=0x80020000; n=(len(data)-0x800)//4
res=[]
for i in range(n):
    a=base+4*i
    w=u32(a)
    if (w>>16)&0xFFFF==0x3c00|0 : pass
    op=w>>26
    imm=w&0xFFFF
    if op in (0x23,0x2b,0x09,0x21,0x25,0x20,0x24,0x29,0x28) and imm==lo:
        # look back up to 12 instr for lui with hi
        for k in range(1,14):
            pw=u32(a-4*k)
            if (pw>>26)==0x0f and (pw&0xFFFF)==hi:
                res.append(a); break
for a in res: print(hex(a))
