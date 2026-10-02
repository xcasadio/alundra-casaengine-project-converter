"""bin.py - own capstone disassembly of the 0x0B / 0x1E / 0x1F / 0x28-0x2B / 0x62 / 0x63 handlers (ALUN_CD.EXE France)."""
import struct
import sys

import capstone

sys.stdout.reconfigure(encoding="utf-8")
EXE = "D:/development/repo/Alundra Remake/Alundra (France)/Alundra (France)_extracted/ALUN_CD.EXE"
B = open(EXE, "rb").read()


def off(a):
    return a - 0x80020000 + 0x800


def word(a):
    return struct.unpack_from("<I", B, off(a))[0]


md = capstone.Cs(capstone.CS_ARCH_MIPS, capstone.CS_MODE_MIPS32 + capstone.CS_MODE_LITTLE_ENDIAN)
TABLE = 0x80098FAC
ops = [int(a, 16) for a in sys.argv[1:]] or [0x0B, 0x1E, 0x1F, 0x28, 0x29, 0x2A, 0x2B, 0x62, 0x63]
for op in ops:
    h = word(TABLE + 4 * op)
    print("==== opcode 0x%02X handler 0x%08X" % (op, h))
    n = 0
    jr = 0
    for ins in md.disasm(B[off(h):off(h) + 0x200], h):
        print("  %08X  %-8s %s" % (ins.address, ins.mnemonic, ins.op_str))
        n += 1
        if ins.mnemonic == "jr" and ins.op_str == "$ra":
            jr += 1
        if jr and ins.mnemonic != "jr":
            # stop after the delay slot of the last jr ra if the next is another function prologue
            pass
        if n > 70 or (jr and n > 2 and ins.address >= h + 8 and ins.mnemonic in ("addiu",) and "sp, sp, -" in ins.op_str):
            break
        if jr >= 1 and op in (0x28, 0x29, 0x2A, 0x2B) and n > 6:
            break
