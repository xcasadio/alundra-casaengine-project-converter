"""my_dscan.py - critic's scan of the native D (touch) and C/E handlers: which of them read the touching entity's attack (+0x224 then +0x1C8) and what do they test?"""
import struct, re
import lib
from capstone import Cs, CS_ARCH_MIPS, CS_MODE_MIPS32, CS_MODE_LITTLE_ENDIAN
md = Cs(CS_ARCH_MIPS, CS_MODE_MIPS32 + CS_MODE_LITTLE_ENDIAN)
MASTER = 0x80098F4C
def table(slot):
    return lib.u32(MASTER + 4*slot)
def handlers(slot, n=256):
    base = table(slot)
    out = {}
    if base == 0: return out
    for c in range(n):
        try:
            a = lib.u32(base + 4*c)
        except Exception:
            break
        if 0x80020000 <= a < 0x800CA800 and a % 4 == 0:
            out.setdefault(a, []).append(c)
    return out
def body(a, maxn=700):
    ins = []
    n = 0
    pend = 0
    while n < maxn:
        w = lib.u32(a + 4*n)
        r = list(md.disasm(w.to_bytes(4, 'little'), a + 4*n))
        ins.append((a+4*n, w, r[0].mnemonic if r else '.word', r[0].op_str if r else ''))
        if r and r[0].mnemonic in ('beq','bne','beqz','bnez','blez','bgtz','bltz','bgez','j'):
            for t in re.findall(r'0x[0-9a-f]{6,8}', r[0].op_str):
                pend = max(pend, int(t, 16))
        n += 1
        if r and r[0].mnemonic == 'jr' and r[0].op_str == 'ra':
            if a + 4*(n+1) > pend:
                w = lib.u32(a + 4*n); r = list(md.disasm(w.to_bytes(4, 'little'), a + 4*n))
                ins.append((a+4*n, w, r[0].mnemonic if r else '.word', r[0].op_str if r else ''))
                break
    return ins
if __name__ == '__main__':
    for slot, nm in ((3, 'D'),):
        hs = handlers(slot, 64)
        print(nm, 'handlers', len(hs))
        for a, codes in sorted(hs.items()):
            ins = body(a)
            txt = ' '.join('%s %s' % (m, o) for _, _, m, o in ins)
            uses224 = '0x224(' in txt
            uses1c8 = '0x1c8(' in txt
            print('D%-12s %08x  %3d ins  reads +0x224:%s  +0x1C8:%s' % (codes, a, len(ins), uses224, uses1c8))
