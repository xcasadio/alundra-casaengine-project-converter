"""Find where the sprite header program codes (header bytes 0x13..0x17) are copied into entity+0x70..0x84. Read-only."""
import sys
import capstone
import exelib

md = capstone.Cs(capstone.CS_ARCH_MIPS, capstone.CS_MODE_MIPS32 + capstone.CS_MODE_LITTLE_ENDIAN)
base = exelib.T_ADDR
code = exelib.DATA[0x800:0x800 + exelib.T_SIZE]
ins = []
import struct
for a in range(0, len(code) - 3, 4):
    for x in md.disasm(code[a:a + 4], base + a):
        ins.append(x)
print(len(ins), "instructions")
# lbu rt, 0x13..0x17 (rs) followed (within 6 insns) by sw rx, 0x70..0x84 (ry)
hits = []
for i, x in enumerate(ins):
    if x.mnemonic == "lbu":
        try:
            off = int(x.op_str.split(",")[1].split("(")[0].strip(), 0)
        except Exception:
            continue
        if 0x13 <= off <= 0x17:
            for y in ins[i + 1:i + 8]:
                if y.mnemonic == "sw":
                    try:
                        off2 = int(y.op_str.split(",")[1].split("(")[0].strip(), 0)
                    except Exception:
                        continue
                    if 0x70 <= off2 <= 0x84:
                        hits.append((x.address, x.mnemonic, x.op_str, y.address, y.mnemonic, y.op_str))
for h in hits:
    print(" ".join(f"{v:#x}" if isinstance(v, int) else str(v) for v in h))
