"""Minimal read-only disassembler of ALUN_CD.EXE (France): file offset = 0x800 + addr - 0x80020000.  python -B dis.py START END"""
import sys
from capstone import Cs, CS_ARCH_MIPS, CS_MODE_MIPS32, CS_MODE_LITTLE_ENDIAN
EXE = 'D:/development/repo/Alundra Remake/Alundra (France)/Alundra (France)_extracted/ALUN_CD.EXE'
_data = open(EXE, 'rb').read()
md = Cs(CS_ARCH_MIPS, CS_MODE_MIPS32 + CS_MODE_LITTLE_ENDIAN)
md.skipdata = True
def off(a): return 0x800 + a - 0x80020000
def dis(a0, a1):
    code = _data[off(a0):off(a1)]
    for i in md.disasm(code, a0):
        yield i
if __name__ == '__main__':
    a0 = int(sys.argv[1], 16); a1 = int(sys.argv[2], 16)
    for i in dis(a0, a1):
        print('%08X: %-8s %s' % (i.address, i.mnemonic, i.op_str))
