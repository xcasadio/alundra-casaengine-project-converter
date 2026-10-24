"""Disassemble a range of ALUN_CD.EXE with capstone (MIPS32 little endian). Read-only.
Usage: python -B dis.py START END   (hex virtual addresses)"""
import sys
import capstone
import exelib

md = capstone.Cs(capstone.CS_ARCH_MIPS, capstone.CS_MODE_MIPS32 + capstone.CS_MODE_LITTLE_ENDIAN)
md.detail = False


def dis(start, end):
    code = exelib.DATA[exelib.off(start):exelib.off(start) + (end - start)]
    for ins in md.disasm(code, start):
        print(f"{ins.address:#010x}  {ins.mnemonic:8s} {ins.op_str}")


if __name__ == "__main__":
    dis(int(sys.argv[1], 16), int(sys.argv[2], 16))
