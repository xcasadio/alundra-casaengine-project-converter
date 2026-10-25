import sys, struct
sys.path.insert(0, r'C:\Users\casad\AppData\Roaming\Python\Python314\site-packages')
import capstone
EXE = r"D:/development/repo/Alundra Remake/Alundra (France)/Alundra (France)_extracted/ALUN_CD.EXE"
D = open(EXE, 'rb').read()
BASE = 0x80020000
md = capstone.Cs(capstone.CS_ARCH_MIPS, capstone.CS_MODE_MIPS32 + capstone.CS_MODE_LITTLE_ENDIAN)
def o(a): return 0x800 + a - BASE
def u8(a): return D[o(a)]
def u16(a): return struct.unpack_from('<H', D, o(a))[0]
def s16(a): return struct.unpack_from('<h', D, o(a))[0]
def u32(a): return struct.unpack_from('<I', D, o(a))[0]
def pr(a, b):
    code = D[o(a): o(a) + (b - a) + 4]
    for i in md.disasm(code, a):
        print('%08x %s %s' % (i.address, i.mnemonic, i.op_str))
