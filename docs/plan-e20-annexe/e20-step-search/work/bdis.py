"""Disassemble a range of ALUN_CD.EXE (France). Read-only. Usage: python -I dis.py START END  (hex)
Adds capstone to sys.path. Prints branch targets as labels."""
import sys, struct
sys.path.insert(0, r"C:/Users/casad/AppData/Roaming/Python/Python314/site-packages")
from capstone import Cs, CS_ARCH_MIPS, CS_MODE_MIPS32, CS_MODE_LITTLE_ENDIAN
EXE = r"D:\development\repo\Alundra Remake\Alundra (France)\Alundra (France)_extracted\ALUN_CD.EXE"
DATA = open(EXE, "rb").read()
BASE = 0x80020000
OFF = 0x800
md = Cs(CS_ARCH_MIPS, CS_MODE_MIPS32 + CS_MODE_LITTLE_ENDIAN)

def dis(a, b):
    o = a - BASE + OFF
    return list(md.disasm(DATA[o:o + (b - a)], a))

if __name__ == "__main__":
    a = int(sys.argv[1], 16); b = int(sys.argv[2], 16)
    for i in dis(a, b):
        print("%08x  %-8s %s" % (i.address, i.mnemonic, i.op_str))
