"""dis.py - disassemble ranges of ALUN_CD.EXE (France) with capstone skipdata.  usage: python -B dis.py START END [START END ...]  (hex addresses)
FACT source: file offset = 0x800 + addr - 0x80020000."""
import sys
import struct
from capstone import Cs, CS_ARCH_MIPS, CS_MODE_MIPS32, CS_MODE_LITTLE_ENDIAN

EXE = r"D:\development\repo\Alundra Remake\Alundra (France)\Alundra (France)_extracted\ALUN_CD.EXE"
DATA = open(EXE, "rb").read()
BASE = 0x80020000
OFF = 0x800
md = Cs(CS_ARCH_MIPS, CS_MODE_MIPS32 + CS_MODE_LITTLE_ENDIAN)
md.skipdata = True


def u32(a):
    return struct.unpack_from("<I", DATA, a - BASE + OFF)[0]


def dis(a, b):
    o = a - BASE + OFF
    return list(md.disasm(DATA[o:o + (b - a)], a))


def show(a, b):
    for ins in dis(a, b):
        print('%08X  %08x  %-8s %s' % (ins.address, u32(ins.address), ins.mnemonic, ins.op_str))
    print()


if __name__ == '__main__':
    args = sys.argv[1:]
    for i in range(0, len(args), 2):
        show(int(args[i], 16), int(args[i + 1], 16))
