"""mydis.py - critic's own listing tool: linear disassembly of a function (capstone), with branch/jump labels. usage: python -B mydis.py 0x80034224 [maxins]"""
import sys, re
import lib
from capstone import Cs, CS_ARCH_MIPS, CS_MODE_MIPS32, CS_MODE_LITTLE_ENDIAN
md = Cs(CS_ARCH_MIPS, CS_MODE_MIPS32 + CS_MODE_LITTLE_ENDIAN)
md.detail = False

def listing(a, maxn=600):
    ins = []
    n = 0
    seen_ret = False
    while n < maxn:
        w = lib.u32(a + 4*n)
        r = list(md.disasm(w.to_bytes(4, 'little'), a + 4*n))
        if r:
            ins.append((a + 4*n, w, r[0].mnemonic, r[0].op_str))
        else:
            ins.append((a + 4*n, w, '.word', '0x%08x' % w))
        n += 1
        if ins[-1][2] == 'jr' and ins[-1][3] == 'ra':
            # include delay slot, then stop if no pending forward branch target beyond
            pend = max([int(t, 16) for (_, _, m, o) in ins for t in re.findall(r'0x[0-9a-f]{6,8}', o) if m.startswith('b') or m in ('j',)] + [0])
            w = lib.u32(a + 4*n)
            r = list(md.disasm(w.to_bytes(4, 'little'), a + 4*n))
            ins.append((a + 4*n, w, r[0].mnemonic if r else '.word', r[0].op_str if r else ''))
            n += 1
            if a + 4*n > pend:
                break
    return ins

def main():
    a = int(sys.argv[1], 16)
    mx = int(sys.argv[2]) if len(sys.argv) > 2 else 600
    ins = listing(a, mx)
    targets = set()
    for (pc, w, m, o) in ins:
        if m.startswith('b') or m == 'j':
            for t in re.findall(r'0x[0-9a-f]{6,8}', o):
                targets.add(int(t, 16))
    for (pc, w, m, o) in ins:
        lab = 'L' if pc in targets else ' '
        print('%08x %s %08x  %-7s %s' % (pc, lab, w, m, o))
main()
