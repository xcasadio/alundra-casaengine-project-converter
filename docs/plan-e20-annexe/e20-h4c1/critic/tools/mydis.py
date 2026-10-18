"""mydis.py - own capstone disassembler of ALUN_CD.EXE (France), independent of the h4 annex tools.
usage: python -B mydis.py START END   (hex addresses, END exclusive)"""
import struct
import sys
import capstone

EXE = r"D:\development\repo\Alundra Remake\Alundra (France)\Alundra (France)_extracted\ALUN_CD.EXE"
DATA = open(EXE, "rb").read()
PC0, GP0, T_ADDR, T_SIZE = struct.unpack_from("<IIII", DATA, 0x10)


def off(a):
    return 0x800 + (a - T_ADDR)


def u32(a):
    return struct.unpack_from("<I", DATA, off(a))[0]


def u16(a):
    return struct.unpack_from("<H", DATA, off(a))[0]


def s16(a):
    return struct.unpack_from("<h", DATA, off(a))[0]


def u8(a):
    return DATA[off(a)]


md = capstone.Cs(capstone.CS_ARCH_MIPS, capstone.CS_MODE_MIPS32 + capstone.CS_MODE_LITTLE_ENDIAN)
md.detail = False


def dis(start, end):
    code = DATA[off(start):off(end)]
    for i in md.disasm(code, start):
        print("%08X  %s %s" % (i.address, i.mnemonic.ljust(8), i.op_str))


if __name__ == "__main__":
    s = int(sys.argv[1], 16)
    e = int(sys.argv[2], 16)
    dis(s, e)
