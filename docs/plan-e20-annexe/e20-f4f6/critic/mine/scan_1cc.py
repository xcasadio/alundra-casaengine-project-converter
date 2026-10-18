"""Critic: scan the whole code image for loads/stores of displacement 0x1CC and the 'biased' 0x164 (slot+0x68 base as in E17), and 0x1CC-0x68.. list stores."""
import re, sys
sys.path.insert(0,'.')
from capstone import Cs, CS_ARCH_MIPS, CS_MODE_MIPS32, CS_MODE_LITTLE_ENDIAN
import mipsdis
data = mipsdis._data
md = mipsdis.md
base = 0x80020000
start = 0x80020000; end = 0x800CA800
code = data[mipsdis.off(start):mipsdis.off(end)]
pat = re.compile(r'(-?0x[0-9a-f]+|-?\d+)\(\$(\w+)\)')
res = {}
for i in md.disasm(code, start):
    m = i.mnemonic
    if m in ('sw','sh','sb','lw','lh','lhu','lb','lbu'):
        mm = pat.search(i.op_str)
        if not mm: continue
        d = int(mm.group(1),16) if 'x' in mm.group(1) else int(mm.group(1))
        if d in (0x1CC, 0x164):
            res.setdefault((d,'store' if m.startswith('s') else 'load'),[]).append(i.address)
for k in sorted(res):
    print(k, len(res[k]), ' '.join('%08X'%a for a in res[k]))
