import sys, struct, capstone, exelib
# find all instructions referencing a given 32-bit absolute address via lui/addiu/lw/sw pairs (very simple linear scan)
target = int(sys.argv[1], 16)
md = capstone.Cs(capstone.CS_ARCH_MIPS, capstone.CS_MODE_MIPS32 + capstone.CS_MODE_LITTLE_ENDIAN)
lo, hi = 0x80010000, 0x80010000 + exelib.T_SIZE
regs = {}
for addr in range(exelib.T_ADDR, exelib.T_ADDR + exelib.T_SIZE, 4):
    try:
        w = exelib.u32(addr)
    except Exception:
        break
    ins = list(md.disasm(struct.pack('<I', w), addr))
    if not ins: continue
    i = ins[0]
    m = i.mnemonic
    ops = i.op_str.replace(' ', '').split(',')
    if m == 'lui':
        regs[ops[0]] = (int(ops[1], 16) << 16)
    elif m in ('addiu','lw','sw','lh','lhu','sh','lb','lbu','sb') :
        try:
            if m == 'addiu':
                rd, rs, imm = ops
                if rs in regs:
                    v = (regs[rs] + int(imm, 16 if imm.startswith(('0x','-0x')) else 10)) & 0xFFFFFFFF
                    if v == target: print(hex(addr), m, i.op_str)
                    regs[rd] = v
            else:
                rt, mem = ops[0], ops[1]
                off, base = mem.rstrip(')').split('(')
                off = int(off, 16 if off.startswith(('0x','-0x')) else 10)
                if base in regs and ((regs[base] + off) & 0xFFFFFFFF) == target:
                    print(hex(addr), m, i.op_str)
                if m.startswith('l'):
                    regs.pop(rt, None)
        except Exception as e:
            pass
