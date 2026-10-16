"""Annotated MIPS disassembly (read-only). Resolves lui+addiu/ori/load/store addresses only while the register is not overwritten."""
import sys
import capstone, exelib
md = capstone.Cs(capstone.CS_ARCH_MIPS, capstone.CS_MODE_MIPS32 + capstone.CS_MODE_LITTLE_ENDIAN)
md.detail = True
STORES = ("sw","sb","sh")
LOADS = ("lw","lbu","lb","lhu","lh")
BR = ("beq","bne","bgez","bgtz","blez","bltz","beqz","bnez","b","j","jal","jr","jalr","bal")
def annotate(s, e):
    code = exelib.DATA[exelib.off(s):exelib.off(s) + (e - s)]
    hi = {}
    out = []
    for ins in md.disasm(code, s):
        note = ""; m = ins.mnemonic; ops = ins.operands
        written = None
        if m == "lui":
            hi[ins.reg_name(ops[0].reg)] = ops[1].imm << 16
            written = None
        elif m in ("addiu", "ori") and len(ops) == 3:
            src = ins.reg_name(ops[1].reg); dst = ins.reg_name(ops[0].reg)
            if src in hi:
                val = (hi[src] + ops[2].imm) & 0xffffffff
                note = f"  ; = {val:#010x}"
                hi[dst] = val
            else:
                hi.pop(dst, None)
        elif m in LOADS or m in STORES:
            mem = ops[1]
            if mem.type == capstone.mips.MIPS_OP_MEM and ins.reg_name(mem.mem.base) in hi:
                val = (hi[ins.reg_name(mem.mem.base)] + mem.mem.disp) & 0xffffffff
                note = f"  ; [{val:#010x}]"
            if m in LOADS:
                hi.pop(ins.reg_name(ops[0].reg), None)
        elif m == "jal":
            note = f"  ; call {ops[0].imm:#010x}"
        elif m not in BR and ops and ops[0].type == capstone.mips.MIPS_OP_REG:
            hi.pop(ins.reg_name(ops[0].reg), None)
        out.append(f"{ins.address:#010x}  {m:7s} {ins.op_str}{note}")
    return "\n".join(out) + "\n"
