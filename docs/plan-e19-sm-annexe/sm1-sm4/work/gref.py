"""Find references to a global address range via lui/addiu pairs and gp-free load/store (read-only).
Usage: python -B gref.py LO_HEX HI_HEX      (virtual addresses, inclusive-exclusive)
Reports instructions `lui rX, hi` followed (<= 12 instructions later, same register not overwritten) by a
load/store/addiu using rX with an immediate so that hi<<16 + imm lies in [LO, HI)."""
import sys, struct
import exelib

lo = int(sys.argv[1], 16)
hi = int(sys.argv[2], 16)
RN = ["zero","at","v0","v1","a0","a1","a2","a3","t0","t1","t2","t3","t4","t5","t6","t7","s0","s1","s2","s3","s4","s5","s6","s7","t8","t9","k0","k1","gp","sp","fp","ra"]
n = (len(exelib.DATA) - 0x800) // 4
words = [struct.unpack_from("<I", exelib.DATA, 0x800 + 4 * i)[0] for i in range(n)]
MEM = {0x20: "lb", 0x21: "lh", 0x23: "lw", 0x24: "lbu", 0x25: "lhu", 0x28: "sb", 0x29: "sh", 0x2B: "sw"}


def s16(x):
    return x - 0x10000 if x >= 0x8000 else x


for i, w in enumerate(words):
    if (w >> 26) == 0x0F:  # lui
        rt = (w >> 16) & 31
        base = (w & 0xFFFF) << 16
        if not (lo - 0x8000 <= base <= hi + 0x8000):
            continue
        for j in range(i + 1, min(i + 13, n)):
            v = words[j]
            op = v >> 26
            rs = (v >> 21) & 31
            rtj = (v >> 16) & 31
            if op in MEM and rs == rt:
                addr = (base + s16(v & 0xFFFF)) & 0xFFFFFFFF
                if lo <= addr < hi:
                    print(f"{exelib.T_ADDR + 4 * j:#010x}  {MEM[op]:3s} {RN[rtj]}, {addr:#010x}   (lui at {exelib.T_ADDR + 4 * i:#010x})")
            elif op == 0x09 and rs == rt:  # addiu
                addr = (base + s16(v & 0xFFFF)) & 0xFFFFFFFF
                if lo <= addr < hi:
                    print(f"{exelib.T_ADDR + 4 * j:#010x}  addiu {RN[rtj]}, {RN[rs]} -> {addr:#010x}   (lui at {exelib.T_ADDR + 4 * i:#010x})")
            # stop when the base register is overwritten by a non-mem/addiu write
            if ((op in (0x09, 0x0F, 0x23, 0x24, 0x25, 0x21, 0x20, 0x0D, 0x0C) and rtj == rt) or (op == 0 and ((v >> 11) & 31) == rt)):
                break
