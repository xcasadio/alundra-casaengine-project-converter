"""Own counter-check: every load/store with displacement 0xCC or 0xD0 in the text segment, grouped by enclosing function start (heuristic: previous 'addiu sp,sp,-N')."""
import re, sys, capstone, exelib
md = capstone.Cs(capstone.CS_ARCH_MIPS, capstone.CS_MODE_MIPS32 + capstone.CS_MODE_LITTLE_ENDIAN)
base = exelib.T_ADDR; data = exelib.DATA
n = (len(data) - 0x800) // 4
want = {0xCC, 0xD0}
res = []
fstart = None
funcs = []
for i in range(n):
    addr = base + 4 * i
    r = list(md.disasm(data[0x800 + 4 * i:0x800 + 4 * i + 4], addr))
    if not r:
        continue
    x = r[0]
    m_ = x.mnemonic
    if m_ == 'addiu' and x.op_str.startswith('$sp, $sp, -'):
        fstart = addr
    mm = re.match(r'(\$\w+), (-?0x[0-9a-fA-F]+|-?\d+)\((\$\w+)\)', x.op_str)
    if mm and m_ in ('lw', 'sw', 'lh', 'lhu', 'sh', 'lb', 'lbu', 'sb'):
        off = int(mm.group(2), 0)
        if off in want:
            res.append((addr, m_, off, mm.group(1), mm.group(3), fstart))
print("total accesses", len(res))
for off in sorted(want):
    print("== off 0x%X" % off)
    for a, m_, o, rt, rb, fs in res:
        if o == off:
            print("  %08X %-3s %s(%s) fn~%s" % (a, m_, rt, rb, hex(fs) if fs else None))
