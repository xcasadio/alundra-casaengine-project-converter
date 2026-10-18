"""scan_refs.py - critic's own static scan of the whole text segment of ALUN_CD.EXE for every instruction that forms an address in
given ranges via lui + (addiu | lw | sw | lh | lhu | lb | lbu | sh | sb) (linear tracking of the last lui per register).
Usage: python -B scan_refs.py LO HI [LO HI ...]  (hex)  -- prints (address, mnemonic, formed address, enclosing function start)
Read-only.
"""
import sys
import capstone
import exelib

md = capstone.Cs(capstone.CS_ARCH_MIPS, capstone.CS_MODE_MIPS32 + capstone.CS_MODE_LITTLE_ENDIAN)
md.detail = False

ranges = [(int(sys.argv[i], 16), int(sys.argv[i + 1], 16)) for i in range(1, len(sys.argv), 2)]
start = exelib.T_ADDR
end = start + exelib.T_SIZE
code = exelib.DATA[0x800:0x800 + exelib.T_SIZE]
NAMES = ['zero', 'at', 'v0', 'v1', 'a0', 'a1', 'a2', 'a3', 't0', 't1', 't2', 't3', 't4', 't5', 't6', 't7',
         's0', 's1', 's2', 's3', 's4', 's5', 's6', 's7', 't8', 't9', 'k0', 'k1', 'gp', 'sp', 'fp', 'ra']
lui = {}
func = None
hits = []
def iter_ins():
    for i in range(0, len(code) - 3, 4):
        for ins in md.disasm(code[i:i+4], start + i):
            yield ins
            break
for ins in iter_ins():
    m = ins.mnemonic
    ops = [o.strip() for o in ins.op_str.split(',')] if ins.op_str else []
    if m == 'addiu' and ops[0] == '$sp' and ops[1] == '$sp' and ops[2].startswith('-'):
        func = ins.address
        lui = {}
    if m == 'lui':
        lui[ops[0]] = int(ops[1], 0) << 16
        continue
    if m in ('lw', 'sw', 'lh', 'lhu', 'lb', 'lbu', 'sh', 'sb') and '(' in ops[1]:
        o, rest = ops[1].split('(')
        base = rest.rstrip(')')
        off = int(o, 0) if o else 0
        if base in lui:
            a = (lui[base] + off) & 0xFFFFFFFF
            for lo, hi in ranges:
                if lo <= a < hi:
                    hits.append((ins.address, m + ' ' + ins.op_str, a, func))
    elif m == 'addiu' and ops[1] in lui:
        a = (lui[ops[1]] + int(ops[2], 0)) & 0xFFFFFFFF
        for lo, hi in ranges:
            if lo <= a < hi:
                hits.append((ins.address, m + ' ' + ins.op_str, a, func))
        lui[ops[0]] = a   # the register now holds the address
        continue
    # invalidate destination of other writes (cheap approximation: any instruction that writes ops[0] for ALU ops)
    if m in ('addu', 'subu', 'or', 'and', 'xor', 'nor', 'slt', 'sltu', 'sll', 'srl', 'sra', 'move', 'lw', 'lh', 'lhu', 'lb', 'lbu', 'ori', 'andi', 'addiu', 'mflo', 'mfhi', 'slti', 'sltiu', 'negu') and ops:
        lui.pop(ops[0], None)
    if m in ('jal', 'jr', 'j'):
        pass
for h in hits:
    print('%#010x  %-40s -> %#010x   func %s' % (h[0], h[1], h[2], hex(h[3]) if h[3] else '?'))
print(len(hits), 'hits;', len(set(h[3] for h in hits)), 'enclosing function starts (approx, by sp-adjust prologue)')
