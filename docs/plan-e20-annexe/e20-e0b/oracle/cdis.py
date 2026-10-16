import sys, struct
from capstone import Cs, CS_ARCH_MIPS, CS_MODE_MIPS32, CS_MODE_LITTLE_ENDIAN
EXE = r"D:\development\repo\Alundra Remake\Alundra (France)\Alundra (France)_extracted\ALUN_CD.EXE"
D = open(EXE,'rb').read()
md = Cs(CS_ARCH_MIPS, CS_MODE_MIPS32+CS_MODE_LITTLE_ENDIAN); md.skipdata=True
def off(a): return 0x800 + a - 0x80020000
def dis(a, b):
    for i in md.disasm(D[off(a):off(b)], a):
        print('%08X %-8s %s' % (i.address, i.mnemonic, i.op_str))
if __name__=='__main__':
    dis(int(sys.argv[1],16), int(sys.argv[2],16))
