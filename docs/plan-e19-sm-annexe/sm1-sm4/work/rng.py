"""Annotated disassembly of an arbitrary range. Usage: python -B rng.py START END [outfile]"""
import sys
import capstone, exelib
md = capstone.Cs(capstone.CS_ARCH_MIPS, capstone.CS_MODE_MIPS32 + capstone.CS_MODE_LITTLE_ENDIAN)
md.detail = True
def annotate(s, e):
    code = exelib.DATA[exelib.off(s):exelib.off(s) + (e - s)]
    hi = {}
    out = []
    for ins in md.disasm(code, s):
        note = ""; m = ins.mnemonic; ops = ins.operands
        if m == "lui":
            hi[ins.reg_name(ops[0].reg)] = ops[1].imm << 16
        elif m in ("addiu", "ori") and len(ops) == 3 and ins.reg_name(ops[1].reg) in hi:
            val = hi[ins.reg_name(ops[1].reg)] + ops[2].imm
            note = f"  ; = {val & 0xffffffff:#010x}"
            hi[ins.reg_name(ops[0].reg)] = val
        elif m in ("lw","lbu","lb","lhu","lh","sw","sb","sh") and ops[1].type == capstone.mips.MIPS_OP_MEM and ins.reg_name(ops[1].mem.base) in hi:
            val = hi[ins.reg_name(ops[1].mem.base)] + ops[1].mem.disp
            note = f"  ; [{val & 0xffffffff:#010x}]"
        elif m == "jal":
            note = f"  ; call {ops[0].imm:#010x}"
        out.append(f"{ins.address:#010x}  {m:7s} {ins.op_str}{note}")
    return "\n".join(out) + "\n"
if __name__ == "__main__":
    t = annotate(int(sys.argv[1],16), int(sys.argv[2],16))
    if len(sys.argv) > 3:
        open(sys.argv[3], "w").write(t)
    print(t)
