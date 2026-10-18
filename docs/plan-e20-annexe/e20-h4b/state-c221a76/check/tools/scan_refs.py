"""scan_refs.py - find the instructions that access a given absolute address through a lui+offset pair in ALUN_CD.EXE (word by word disassembly).
usage: scan_refs.py ADDR [ADDR...]"""
import sys, re, struct
import capstone, exelib
md = capstone.Cs(capstone.CS_ARCH_MIPS, capstone.CS_MODE_MIPS32 + capstone.CS_MODE_LITTLE_ENDIAN)
targets = [int(a, 16) for a in sys.argv[1:]]
base = exelib.T_ADDR
data = exelib.DATA
n = (len(data) - 0x800) // 4
lui = {}
hits = {t: [] for t in targets}
for i in range(n):
    addr = base + 4 * i
    w = data[0x800 + 4 * i:0x800 + 4 * i + 4]
    r = list(md.disasm(w, addr))
    if not r:
        continue
    x = r[0]
    m = x.mnemonic
    ops = [o.strip() for o in x.op_str.split(',')] if x.op_str else []
    if m == 'lui' and len(ops) == 2:
        lui[ops[0]] = (int(ops[1], 0) << 16, i)
        continue
    ea = None
    if m in ('addiu', 'ori') and len(ops) == 3:
        src = ops[1]
        if src in lui and i - lui[src][1] < 16:
            off = int(ops[2], 0)
            ea = ((lui[src][0] + off) if m == 'addiu' else (lui[src][0] | off)) & 0xFFFFFFFF
    elif m in ('lw', 'sw', 'lh', 'lhu', 'sh', 'lb', 'lbu', 'sb') and len(ops) == 2:
        mm = re.match(r'(-?0x[0-9a-fA-F]+|-?\d+)\((\$\w+)\)', ops[1])
        if mm and mm.group(2) in lui and i - lui[mm.group(2)][1] < 16:
            ea = (lui[mm.group(2)][0] + int(mm.group(1), 0)) & 0xFFFFFFFF
    if ea in hits:
        hits[ea].append((addr, m, x.op_str))
for t, h in hits.items():
    print('%08x: %d refs' % (t, len(h)))
    for a, m, o in h:
        print('   %08x %s %s' % (a, m, o))
