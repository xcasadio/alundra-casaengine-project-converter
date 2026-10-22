import sys
sys.path.insert(0, '.')
import mydis as m

# find every load/store whose effective address (lui + displacement within the previous instructions) hits the pad block 0x80126F18..0x80126F40
N = (len(m.D) - m.HDR) // 4
ins = []
for k in range(0, N, 4096):
    ins.extend(m.dis(m.BASE + 4 * k, min(4096, N - k)))
hits = []
for idx, i in enumerate(ins):
    if i.mnemonic in ('lw', 'lh', 'lhu', 'lb', 'lbu', 'sw', 'sh', 'sb'):
        ops = i.op_str.replace(' ', '')
        try:
            rt, rest = ops.split(',', 1)
            off, base = rest.split('(')
            base = base.rstrip(')')
            off = int(off, 16) if off else 0
        except Exception:
            continue
        for j in range(idx - 1, max(0, idx - 40), -1):
            p = ins[j]
            if p.mnemonic == 'lui' and p.op_str.replace(' ', '').startswith(base + ','):
                hi = int(p.op_str.replace(' ', '').split(',')[1], 16)
                addr = ((hi << 16) + off) & 0xFFFFFFFF
                if 0x80126F18 <= addr <= 0x80126F40:
                    hits.append((hex(i.address), i.mnemonic, i.op_str, hex(addr)))
                break
            if p.mnemonic in ('jr', 'jal', 'jalr'):
                break
            if p.op_str.replace(' ', '').startswith(base + ',') and p.mnemonic not in ('sw', 'sh', 'sb'):
                break
for h in hits:
    print(h)
