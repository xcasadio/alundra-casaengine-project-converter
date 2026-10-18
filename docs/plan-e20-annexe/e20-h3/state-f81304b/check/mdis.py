#!/usr/bin/env python3
"""Counter-check helper: read-only disassembly of a range of ALUN_CD.EXE (France) with capstone.
Usage: python -B dis.py <start_hex> <end_hex>"""
import struct
import sys

from capstone import CS_ARCH_MIPS, CS_MODE_32, CS_MODE_LITTLE_ENDIAN, Cs

EXE_PATH = r"D:\development\repo\Alundra Remake\Alundra (France)\Alundra (France)_extracted\ALUN_CD.EXE"
with open(EXE_PATH, "rb") as f:
    DATA = f.read()
PC0, GP0, T_ADDR, T_SIZE = struct.unpack_from("<IIII", DATA, 0x10)
md = Cs(CS_ARCH_MIPS, CS_MODE_32 | CS_MODE_LITTLE_ENDIAN)
md.detail = False


def dis(a, b):
    code = DATA[0x800 + (a - T_ADDR): 0x800 + (b - T_ADDR)]
    for i in md.disasm(code, a):
        print(f"{i.address:#010x}  {i.mnemonic:<8} {i.op_str}")


if __name__ == "__main__":
    dis(int(sys.argv[1], 16), int(sys.argv[2], 16))
