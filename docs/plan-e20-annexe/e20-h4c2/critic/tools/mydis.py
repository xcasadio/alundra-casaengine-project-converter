"""My own disassembly helper (capstone) for ALUN_CD.EXE (France). Read-only."""
import struct, sys
import capstone

EXE = r"D:\development\repo\Alundra Remake\Alundra (France)\Alundra (France)_extracted\ALUN_CD.EXE"
DATA = open(EXE, "rb").read()
assert DATA[:8] == b"PS-X EXE"
PC0, GP0, TADDR, TSIZE = struct.unpack_from("<IIII", DATA, 0x10)

def off(a):
    o = 0x800 + (a - TADDR)
    if not (0x800 <= o < len(DATA)):
        raise ValueError(hex(a))
    return o

def u32(a): return struct.unpack_from("<I", DATA, off(a))[0]
def s32(a): return struct.unpack_from("<i", DATA, off(a))[0]
def u16(a): return struct.unpack_from("<H", DATA, off(a))[0]
def s16(a): return struct.unpack_from("<h", DATA, off(a))[0]
def u8(a): return DATA[off(a)]

md = capstone.Cs(capstone.CS_ARCH_MIPS, capstone.CS_MODE_MIPS32 | capstone.CS_MODE_LITTLE_ENDIAN)
md.detail = False

def dis(start, end, out=sys.stdout):
    code = DATA[off(start):off(end)]
    for ins in md.disasm(code, start):
        print(f"{ins.address:08x}: {ins.bytes[::-1].hex()}  {ins.mnemonic:8s} {ins.op_str}", file=out)

if __name__ == "__main__":
    a = int(sys.argv[1], 16); b = int(sys.argv[2], 16)
    dis(a, b)
