import struct, sys
from capstone import Cs, CS_ARCH_MIPS, CS_MODE_MIPS32, CS_MODE_LITTLE_ENDIAN
EXE = r"D:\development\repo\Alundra Remake\Alundra (France)\Alundra (France)_extracted\ALUN_CD.EXE"
D = open(EXE, "rb").read()
BASE = 0x80020000
HDR = 0x800
def off(a): return a - BASE + HDR
def u32(a): return struct.unpack_from("<I", D, off(a))[0]
md = Cs(CS_ARCH_MIPS, CS_MODE_MIPS32 + CS_MODE_LITTLE_ENDIAN)
md.detail = False
def dis(a, n):
    return list(md.disasm(D[off(a):off(a)+4*n], a))
def show(a, n):
    for i in dis(a, n):
        print("%08x  %-8s %s" % (i.address, i.mnemonic, i.op_str))
if __name__ == "__main__":
    mode = sys.argv[1]
    if mode == "table":
        for op in sys.argv[2:]:
            o = int(op, 16)
            print("op %02X -> %08x" % (o, u32(0x80098FAC + 4 * o)))
    elif mode == "dis":
        a = int(sys.argv[2], 16); n = int(sys.argv[3])
        show(a, n)
