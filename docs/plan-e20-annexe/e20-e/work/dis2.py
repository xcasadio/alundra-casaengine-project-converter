"""Disassemble with labels. Usage: python -B dis2.py START [END]  (hex). If END omitted, stop after the last jr ra reached with no pending forward branch."""
import sys, struct
import capstone
import exelib

md = capstone.Cs(capstone.CS_ARCH_MIPS, capstone.CS_MODE_MIPS32 + capstone.CS_MODE_LITTLE_ENDIAN)
md.detail = True

def decode(start, end):
    code = exelib.DATA[exelib.off(start):exelib.off(start) + (end - start)]
    return list(md.disasm(code, start))

def find_end(start, limit=0x2000):
    # linear scan: end = after delay slot of a `jr ra` when no branch targets beyond it
    ins = decode(start, start + limit)
    maxtarget = start
    for i, x in enumerate(ins):
        m = x.mnemonic
        if m in ("beq","bne","blez","bgtz","bltz","bgez","beqz","bnez","b","bal","bltzal","bgezal","beql","bnel","blezl","bgtzl"):
            t = x.op_str.split(",")[-1].strip()
            try:
                tv = int(t, 16)
                if tv > maxtarget: maxtarget = tv
            except: pass
        if m == "j":
            try:
                tv = int(x.op_str,16)
                if start <= tv < start+limit and tv > maxtarget: maxtarget = tv
            except: pass
        if m == "jr" and x.op_str == "$ra" and x.address >= maxtarget:
            return x.address + 8
    return start + limit

def run(start, end=None):
    if end is None: end = find_end(start)
    ins = decode(start, end)
    targets = set()
    for x in ins:
        m = x.mnemonic
        if m in ("beq","bne","blez","bgtz","bltz","bgez","beqz","bnez","b","bal","beql","bnel","j"):
            t = x.op_str.split(",")[-1].strip()
            try: targets.add(int(t,16))
            except: pass
    for x in ins:
        if x.address in targets: print(f"L_{x.address:08x}:")
        print(f"{x.address:08x}  {x.mnemonic:7s} {x.op_str}")
    return end

if __name__ == "__main__":
    s = int(sys.argv[1],16)
    e = int(sys.argv[2],16) if len(sys.argv)>2 else None
    end = run(s,e)
    print(f"; end {end:08x}")
