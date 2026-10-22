import struct,sys
from capstone import *
p=r"D:/development/repo/Alundra Remake/Alundra (France)/Alundra (France)_extracted/ALUN_CD.EXE"
d=open(p,'rb').read()
BASE=0x80020000
def rd(a,n): 
    o=a-BASE+0x800
    return d[o:o+n]
def w(a): return struct.unpack('<I',rd(a,4))[0]
md=Cs(CS_ARCH_MIPS,CS_MODE_MIPS32+CS_MODE_LITTLE_ENDIAN)
md.detail=False
def dis(a,n):
    code=rd(a,n)
    for i in md.disasm(code,a):
        print("%08x  %08x  %s %s"%(i.address,struct.unpack('<I',i.bytes)[0],i.mnemonic,i.op_str))
if __name__=="__main__":
    for op in (0x3C,0x3D,0x60,0x61):
        h=w(0x80098FAC+4*op)
        print("== op %02x -> handler %08x"%(op,h))
        # disassemble until jr ra + delay slot
        a=h; 
        # find end: first 'jr ra' then +8
        code=rd(h,0x200)
        cnt=0
        out=[]
        for i in md.disasm(code,h):
            out.append(i)
        end=None
        for k,i in enumerate(out):
            if i.mnemonic=='jr' and i.op_str=='$ra':
                end=k+1;break
        for i in out[:end+1]:
            print("%08x  %08x  %s %s"%(i.address,struct.unpack('<I',i.bytes)[0],i.mnemonic,i.op_str))
