import struct
from capstone import Cs, CS_ARCH_MIPS, CS_MODE_MIPS32, CS_MODE_LITTLE_ENDIAN
EXE = r"D:\development\repo\Alundra Remake\Alundra (France)\Alundra (France)_extracted\ALUN_CD.EXE"
RAW = open(EXE, "rb").read()
TBASE = struct.unpack_from('<I', RAW, 0x18)[0]
TSIZE = struct.unpack_from('<I', RAW, 0x1C)[0]
TEXT = RAW[0x800:0x800 + TSIZE]
md = Cs(CS_ARCH_MIPS, CS_MODE_MIPS32 + CS_MODE_LITTLE_ENDIAN)
md.detail = False
def rd32(a): return struct.unpack_from('<I', TEXT, a - TBASE)[0]
def rd16(a): return struct.unpack_from('<H', TEXT, a - TBASE)[0]
def rd8(a): return TEXT[a - TBASE]
def dis(a, n):
    out = []
    for k in range(n):
        w = rd32(a + 4 * k)
        r = list(md.disasm(struct.pack('<I', w), a + 4 * k))
        if r: out.append((a + 4 * k, w, r[0].mnemonic, r[0].op_str))
        else: out.append((a + 4 * k, w, '.word', '0x%08x' % w))
    return out
def show(a, n):
    for (ad, w, m, o) in dis(a, n):
        print('%08x  %08x  %-8s %s' % (ad, w, m, o))
if __name__ == '__main__':
    import sys
    show(int(sys.argv[1], 0), int(sys.argv[2], 0))
