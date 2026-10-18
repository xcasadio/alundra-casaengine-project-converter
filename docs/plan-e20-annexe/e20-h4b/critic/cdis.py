"""cdis.py - critic's disassembler (capstone) of the ALUN_CD.EXE image. Usage: python -B cdis.py START END"""
import sys
from capstone import Cs, CS_ARCH_MIPS, CS_MODE_MIPS32, CS_MODE_LITTLE_ENDIAN
from cmips import IMG, BASE, TEXT_OFF, IMG_END

_cs = Cs(CS_ARCH_MIPS, CS_MODE_MIPS32 + CS_MODE_LITTLE_ENDIAN)
_cs.detail = False


def dis(start, end):
    code = IMG[start - BASE + TEXT_OFF:end - BASE + TEXT_OFF]
    out = []
    for i in _cs.disasm(code, start):
        out.append((i.address, i.mnemonic, i.op_str))
    return out


def dump(start, end):
    for a, m, o in dis(start, end):
        print("%08x  %-8s %s" % (a, m, o))


if __name__ == "__main__":
    s = int(sys.argv[1], 16)
    e = int(sys.argv[2], 16)
    dump(s, e)
