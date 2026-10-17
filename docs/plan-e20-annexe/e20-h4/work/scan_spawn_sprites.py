"""scan_spawn_sprites.py - for every call of SpawnWarpEntity (jal 0x80039F58) find the immediate loaded into a2 (sprite index) and a1 (bank flag) around it. Heuristic (look back 14 words and the delay slot)."""
import re, struct, capstone, exelib
md = capstone.Cs(capstone.CS_ARCH_MIPS, capstone.CS_MODE_MIPS32 + capstone.CS_MODE_LITTLE_ENDIAN)
T = 0x80039F58
sites = []
for a in range(0x80020000, 0x800CA800, 4):
    w = exelib.u32(a)
    if (w >> 26) == 3 and (((a + 4) & 0xF0000000) | ((w & 0x3FFFFFF) << 2)) == T:
        sites.append(a)
def ins(a):
    r = list(md.disasm(exelib.DATA[exelib.off(a):exelib.off(a) + 4], a))
    return r[0] if r else None
out = {}
for s in sites:
    a2 = None
    for k in range(-14, 2):
        x = ins(s + 4 * k)
        if x is None: continue
        if x.mnemonic in ('addiu', 'ori') and x.op_str.startswith('$a2, $zero'):
            a2 = int(x.op_str.split(',')[2], 0)
    out[s] = a2
print(len(sites), 'call sites')
from collections import Counter
c = Counter(v for v in out.values())
print('a2 immediates:', sorted((k if k is not None else -1, n) for k, n in c.items()))
for s, v in sorted(out.items()):
    if v in (16, 17, 18, 19, 20, 21, 22, 23, 24):
        print('  %08x -> %s' % (s, v))
print('unresolved:', ' '.join('%08x' % s for s, v in out.items() if v is None))
