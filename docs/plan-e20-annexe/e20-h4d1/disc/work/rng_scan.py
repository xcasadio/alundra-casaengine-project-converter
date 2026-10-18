"""rng_scan.py (h4d1 discovery, read-only on the binary): walks the static call graph from a root function of ALUN_CD.EXE (France) and reports,
per reachable function, whether it touches the generator word 0x80098708 (lui 0x800A + offset -0x78F8) or the multiplier constant 0x7D2B89DD.
usage: python -B rng_scan.py ROOT [DEPTH]"""
import sys
import struct
import capstone
import exelib

md = capstone.Cs(capstone.CS_ARCH_MIPS, capstone.CS_MODE_MIPS32 + capstone.CS_MODE_LITTLE_ENDIAN)
md.detail = True


def func_ins(start, limit=4000):
    """linear scan from start to the final jr $ra (after the farthest forward branch target)"""
    out = []
    maxt = start
    a = start
    while len(out) < limit:
        o = exelib.off(a)
        r = list(md.disasm(exelib.DATA[o:o + 4], a))
        if not r:
            out.append(None)
            a += 4
            continue
        i = r[0]
        out.append(i)
        m = i.mnemonic
        if m in ('beq', 'bne', 'beqz', 'bnez', 'blez', 'bgtz', 'bltz', 'bgez', 'b', 'bal', 'bltzal', 'bgezal', 'j'):
            try:
                t = int(i.op_str.split(',')[-1].strip(), 0)
                if t > maxt and t - start < 0x2000:
                    maxt = t
            except Exception:
                pass
        if m == 'jr' and 'ra' in i.op_str and a + 8 > maxt:
            # include delay slot
            o = exelib.off(a + 4)
            r2 = list(md.disasm(exelib.DATA[o:o + 4], a + 4))
            out.append(r2[0] if r2 else None)
            break
        a += 4
    return out


def scan(root, depth):
    seen = {}
    order = [(root, 0)]
    while order:
        f, d = order.pop(0)
        if f in seen or d > depth:
            continue
        ins = func_ins(f)
        info = dict(depth=d, n=len(ins), rng=False, calls=[], lui_800a=False, jalr=0)
        luis = {}
        for i in ins:
            if i is None:
                continue
            m = i.mnemonic
            ops = i.op_str
            if m == 'lui':
                reg, imm = [x.strip() for x in ops.split(',')]
                luis[reg] = int(imm, 0)
                if int(imm, 0) == 0x800a:
                    info['lui_800a'] = True
                if int(imm, 0) == 0x7d2b:
                    info['rng'] = True
            if m in ('lw', 'sw') and '-0x78f8' in ops:
                info['rng'] = True
            if m in ('jalr',) or (m == 'jr' and 'ra' not in ops):
                info['jalr'] += 1
            if m == 'jal':
                t = int(ops, 0)
                info['calls'].append(t)
                if t not in seen:
                    order.append((t, d + 1))
        seen[f] = info
    return seen


if __name__ == '__main__':
    root = int(sys.argv[1], 0)
    depth = int(sys.argv[2]) if len(sys.argv) > 2 else 4
    res = scan(root, depth)
    for f, info in sorted(res.items(), key=lambda kv: (kv[1]['depth'], kv[0])):
        print('%08X depth %d ins %4d rng=%s indirect=%d calls=%s' % (f, info['depth'], info['n'], info['rng'], info['jalr'], ' '.join('%08X' % c for c in info['calls'])))
    print('functions reachable:', len(res), ' touching the generator:', [hex(f) for f, i in res.items() if i['rng']])
