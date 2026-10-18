"""my_loaddelay.py - critic: does any of the 12(+3) functions read a freshly loaded register in the very next instruction (R3000 load delay slot, not emulated by the interpreter)?"""
import re, lib
from capstone import Cs, CS_ARCH_MIPS, CS_MODE_MIPS32, CS_MODE_LITTLE_ENDIAN
md = Cs(CS_ARCH_MIPS, CS_MODE_MIPS32 + CS_MODE_LITTLE_ENDIAN)
FUNCS = {'start_beans': (0x80034224, 0x80034320), 'start_bomb': (0x8003453C, 0x80034680), 'drv_beans': (0x80035204, 0x80035260), 'drv_bomb': (0x800352C4, 0x80035320),
         'cancel_beans': (0x80034ACC, 0x80034B54), 'cancel_bomb': (0x80034B54, 0x80034BDC), 'dispatch': (0x800350C0, 0x800351B8), 'C62': (0x8007A978, 0x8007AC60),
         'D30': (0x8007E79C, 0x8007E7E4), 'E12': (0x8007F27C, 0x8007F30C), 'C73': (0x8007BB30, 0x8007BB9C), 'E21': (0x8007F7CC, 0x8007F878), 'C74': (0x8007BB9C, 0x8007BD8C), 'A20': (0x800619DC, 0x800619E8),
         'CATE': (0x8003A7B0, 0x8003A9E0), 'ComputeNewHp': (0x8003A374, 0x8003A59C), 'Resolve': (0x8004464C, 0x80044820)}
LOADS = ('lw', 'lh', 'lhu', 'lb', 'lbu', 'lwl', 'lwr')
REG = r'\$(\w+)'
total = 0
for nm, (a, b) in FUNCS.items():
    prev = None
    for x in range(a, b, 4):
        w = lib.u32(x); r = list(md.disasm(w.to_bytes(4, 'little'), x))[0]
        if prev and prev.mnemonic in LOADS:
            dst = re.match(REG, prev.op_str).group(1)
            ops = r.op_str
            # registers used as SOURCES in r (any register mention except first operand for non-store/branch ops)
            regs = re.findall(REG, ops)
            srcs = regs[1:] if r.mnemonic not in ('sw', 'sh', 'sb', 'beq', 'bne', 'beqz', 'bnez', 'blez', 'bgtz', 'bltz', 'bgez', 'mult', 'multu', 'div', 'divu', 'jr', 'jalr', 'swl', 'swr') else regs
            if r.mnemonic in ('jr',): srcs = regs
            if dst != 'zero' and dst in srcs:
                print('LOAD-DELAY HAZARD in %s: %08x %s %s  ->  %08x %s %s' % (nm, prev.address, prev.mnemonic, prev.op_str, r.address, r.mnemonic, r.op_str)); total += 1
        prev = r
print('hazards found:', total)
