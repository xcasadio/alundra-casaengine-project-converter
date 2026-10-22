"""Annotated disassembly of the interpreter handlers (read-only on the EXE).
Usage: python -B listing.py OPCODE_HEX [OPCODE_HEX ...]   (writes listings/op_XX.txt and prints)"""
import sys, os, struct
import capstone
import exelib

md = capstone.Cs(capstone.CS_ARCH_MIPS, capstone.CS_MODE_MIPS32 + capstone.CS_MODE_LITTLE_ENDIAN)
md.detail = True

TABLE = 0x80098FAC
H = {o: exelib.u32(TABLE + 4 * o) for o in range(256)}
STARTS = sorted(set(H.values()))

def span(op):
    s = H[op]
    nxt = [a for a in STARTS if a > s]
    return s, (nxt[0] if nxt else s + 0x200)

import annot
def listing(op):
    s, e = span(op)
    return s, e, annot.annotate(s, e).splitlines()
def _old_listing(op):
    s, e = span(op)
    code = exelib.DATA[exelib.off(s):exelib.off(s) + (e - s)]
    lines = []
    hi = {}
    for ins in md.disasm(code, s):
        note = ""
        m = ins.mnemonic
        ops = ins.operands
        if m == "lui":
            hi[ins.reg_name(ops[0].reg)] = ops[1].imm << 16
        elif m in ("addiu", "ori") and len(ops) == 3 and ins.reg_name(ops[1].reg) in hi:
            val = hi[ins.reg_name(ops[1].reg)] + (ops[2].imm if m == "addiu" else ops[2].imm)
            note = f"  ; = {val & 0xffffffff:#010x}"
            hi[ins.reg_name(ops[0].reg)] = val
        elif m in ("lw", "lbu", "lb", "lhu", "lh", "sw", "sb", "sh") and ops[1].type == capstone.mips.MIPS_OP_MEM and ins.reg_name(ops[1].mem.base) in hi:
            val = hi[ins.reg_name(ops[1].mem.base)] + ops[1].mem.disp
            note = f"  ; [{val & 0xffffffff:#010x}]"
        elif m == "jal":
            note = f"  ; call {ops[0].imm:#010x}"
        lines.append(f"{ins.address:#010x}  {m:7s} {ins.op_str}{note}")
    return s, e, lines

if __name__ == "__main__":
    os.makedirs("listings", exist_ok=True)
    for a in sys.argv[1:]:
        op = int(a, 16)
        s, e, lines = listing(op)
        txt = f"; opcode {op:#04x}  handler {s:#010x}..{e:#010x} ({(e-s)//4} words)\n" + "\n".join(lines) + "\n"
        with open(f"listings/op_{op:02X}.txt", "w") as f:
            f.write(txt)
        print(txt)
