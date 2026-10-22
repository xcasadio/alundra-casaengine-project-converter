import sys, capstone, exelib
md = capstone.Cs(capstone.CS_ARCH_MIPS, capstone.CS_MODE_MIPS32 + capstone.CS_MODE_LITTLE_ENDIAN)
md.detail = True
def insn_at(a):
    code = exelib.DATA[exelib.off(a):exelib.off(a)+4]
    return next(md.disasm(code, a))
def reach(start, maxn=400):
    """follow control flow, return sorted list of addresses (instructions) reachable inside the function (no jal follow)"""
    seen=set(); todo=[start]
    while todo:
        a=todo.pop()
        while a not in seen and len(seen)<maxn:
            seen.add(a)
            i=insn_at(a)
            m=i.mnemonic
            if m in ('jr',):  # return (delay slot)
                seen.add(a+4); break
            if m=='j':
                seen.add(a+4)
                tgt=int(i.op_str,16)
                todo.append(tgt); break
            if m in ('beq','bne','beqz','bnez','blez','bgtz','bltz','bgez','bgezal','bltzal','b','bal'):
                tgt=int(i.op_str.split(',')[-1].strip(),16)
                seen.add(a+4)
                todo.append(tgt)
                if m in ('b',): break
                a+=8; continue
            a+=4
    return sorted(seen)
def listing(start):
    out=[]
    for a in reach(start):
        i=insn_at(a)
        out.append(f"{a:#010x}  {i.mnemonic:7s} {i.op_str}")
    return "\n".join(out)
if __name__=='__main__':
    print(listing(int(sys.argv[1],16)))
