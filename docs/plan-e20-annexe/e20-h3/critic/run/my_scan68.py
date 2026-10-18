"""my_scan68.py - critic: every place that loads +0x68 (sprite kind) of some entity and compares it with a small literal (1 or 2 = bomb/beans, or 0x199 pot), anywhere in the image."""
import struct, re
import lib
from capstone import Cs, CS_ARCH_MIPS, CS_MODE_MIPS32, CS_MODE_LITTLE_ENDIAN
md = Cs(CS_ARCH_MIPS, CS_MODE_MIPS32 + CS_MODE_LITTLE_ENDIAN)
N = (len(lib.DATA) - lib.OFF) // 4
ins = []
for i in range(N):
    w = struct.unpack_from('<I', lib.DATA, lib.OFF + 4*i)[0]
    r = list(md.disasm(w.to_bytes(4, 'little'), lib.BASE + 4*i))
    ins.append(r[0] if r else None)
def func_start(i):
    j = i
    while j > 0:
        x = ins[j]
        if x and x.mnemonic == 'addiu' and x.op_str.startswith('$sp, $sp, -'): return lib.BASE + 4*j
        j -= 1
    return None
out = []
for i, x in enumerate(ins):
    if x and x.mnemonic == 'lw' and re.search(r', 0x68\(\$', x.op_str):
        dst = re.match(r'\$(\w+)', x.op_str).group(1)
        window = ins[i+1:i+8]
        lits = {}
        for y in window:
            if y is None: continue
            m1 = re.match(r'addiu \$(\w+), \$zero, (-?0x[0-9a-f]+|-?\d+)$', y.mnemonic + ' ' + y.op_str)
            if m1: lits[m1.group(1)] = int(m1.group(2), 0)
            if y.mnemonic in ('beq', 'bne'):
                a, b = re.findall(r'\$(\w+)', y.op_str)[:2]
                if (a == dst and b in lits) or (b == dst and a in lits):
                    out.append((x.address, hex(func_start(i) or 0), lits[b if a == dst else a], y.mnemonic))
            if y.mnemonic in ('sltiu', 'slti') and dst in y.op_str:
                pass
        # direct compare with immediate via xori/sltiu patterns skipped
for o in out:
    print('load at %08x (func %s): compared with literal %d via %s' % o)
print(len(out), 'sites')
