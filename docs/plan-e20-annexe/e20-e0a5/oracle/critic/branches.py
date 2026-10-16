import struct,sys
from capstone import *
EXE = r"D:\development\repo\Alundra Remake\Alundra (France)\Alundra (France)_extracted\ALUN_CD.EXE"
D = open(EXE,'rb').read()
BASE=0x80020000; OFF=0x800
md=Cs(CS_ARCH_MIPS,CS_MODE_MIPS32+CS_MODE_LITTLE_ENDIAN); md.skipdata=True
def dis(a,n):
    o=a-BASE+OFF
    return list(md.disasm(D[o:o+4*n],a))
lo=int(sys.argv[1],16); hi=int(sys.argv[2],16)
for i in dis(lo,(hi-lo)//4):
    m=i.mnemonic
    if m in ('j','jal','jr','jalr') or m.startswith('b'):
        print('%08x %s %s'%(i.address,m,i.op_str))
