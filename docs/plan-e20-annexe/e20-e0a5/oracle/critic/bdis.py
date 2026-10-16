import sys
from capstone import *
EXE = r"D:\development\repo\Alundra Remake\Alundra (France)\Alundra (France)_extracted\ALUN_CD.EXE"
D = open(EXE,'rb').read()
BASE=0x80020000; OFF=0x800
md=Cs(CS_ARCH_MIPS,CS_MODE_MIPS32+CS_MODE_LITTLE_ENDIAN); md.skipdata=True
lo=int(sys.argv[1],16); hi=int(sys.argv[2],16)
o=lo-BASE+OFF
for i in md.disasm(D[o:o+(hi-lo)],lo):
    print('%08x  %s %s'%(i.address,i.mnemonic,i.op_str))
