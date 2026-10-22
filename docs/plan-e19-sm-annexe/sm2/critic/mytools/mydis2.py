import struct, sys
from capstone import *
EXE = r"D:\development\repo\Alundra Remake\Alundra (France)\Alundra (France)_extracted\ALUN_CD.EXE"
d = open(EXE,'rb').read()
taddr=0x80020000
def off(a): return 0x800 + (a - taddr)
md = Cs(CS_ARCH_MIPS, CS_MODE_MIPS32 + CS_MODE_LITTLE_ENDIAN)
a=int(sys.argv[1],16); b=int(sys.argv[2],16)
for i in md.disasm(d[off(a):off(b)], a):
    print("%08x  %s %s" % (i.address, i.mnemonic, i.op_str))
