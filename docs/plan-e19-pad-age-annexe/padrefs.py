import sys, re
import lib
ins = lib.all_ins()
res = []
for k,i in enumerate(ins):
    if i.mnemonic in ('lhu','lh','lw','lbu','lb','sh','sw','sb','addiu','ori'):
        m = re.match(r'(\$\w+), (-?0x[0-9a-f]+|-?\d+)\((\$\w+)\)', i.op_str) if i.mnemonic not in ('addiu','ori') else re.match(r'(\$\w+), (\$\w+), (-?0x[0-9a-f]+|-?\d+)$', i.op_str)
        if not m: continue
        if i.mnemonic in ('addiu','ori'):
            imm = int(m.group(3),0); base = m.group(2)
        else:
            imm = int(m.group(2),0); base = m.group(3)
        for hi, off in ((0x8012, imm), (0x8013, imm)):
            t = (hi<<16) + off
            if 0x80126F18 <= t <= 0x80126F40:
                for j in range(k-1, max(0,k-14), -1):
                    p = ins[j]
                    if p.mnemonic == 'lui' and p.op_str.startswith(base+',') and p.op_str.endswith('0x%x' % hi):
                        res.append((i.address, i.mnemonic, i.op_str, t)); break
for a,mn,op,t in res:
    print('%08x %-5s %-28s -> %08x' % (a,mn,op,t))
