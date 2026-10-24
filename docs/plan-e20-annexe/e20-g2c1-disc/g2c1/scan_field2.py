"""scan_field2.py OFFSET  - like scan_field.py but ALSO finds accesses through a pre-biased pointer: an access imm(reg) where, within the 160 instructions
before it, reg was loaded by 'addiu reg, X, B' (or 'addiu reg, reg, B') and imm + B == OFFSET.  Read-only.
Run: python -B -I -X utf8 scan_field2.py 0x18C"""
import bisect, re, struct, sys
sys.path.insert(0, r"C:\Users\casad\AppData\Roaming\Python\Python314\site-packages")
import capstone
EXE = r"D:\development\repo\Alundra Remake\Alundra (France)\Alundra (France)_extracted\ALUN_CD.EXE"
DATA = open(EXE, "rb").read()
BASE = struct.unpack_from('<I', DATA, 0x18)[0]
OFF = 0x800
md = capstone.Cs(capstone.CS_ARCH_MIPS, capstone.CS_MODE_MIPS32 + capstone.CS_MODE_LITTLE_ENDIAN)
target = int(sys.argv[1], 16)
lo, hi = 0x8002E000, 0x800F0000
N = (len(DATA) - OFF) // 4
ins = [None] * N
for k in range(0, N, 8192):
    n = min(8192, N - k)
    chunk = DATA[OFF + 4 * k: OFF + 4 * (k + n)]
    for i in md.disasm(chunk, BASE + 4 * k):
        ins[(i.address - BASE) // 4] = i
pro = re.compile(r'^\$sp, \$sp, -')
starts = [i.address for i in ins if i is not None and i.mnemonic == 'addiu' and pro.match(i.op_str)]
memre = re.compile(r'^\$(\w+), (-?0x[0-9a-f]+|-?\d+)\(\$(\w+)\)$')
addre = re.compile(r'^\$(\w+), \$(\w+), (-?0x[0-9a-f]+|-?\d+)$')
def num(s): return int(s, 16) if 'x' in s else int(s)
for idx, i in enumerate(ins):
    if i is None or i.mnemonic not in ('lw', 'sw', 'lh', 'lhu', 'sh', 'lb', 'lbu', 'sb'):
        continue
    if not (lo <= i.address < hi):
        continue
    m = memre.match(i.op_str)
    if not m:
        continue
    imm = num(m.group(2)); reg = m.group(3)
    f = starts[bisect.bisect_right(starts, i.address) - 1]
    if imm == target:
        kind = 'direct'
    else:
        kind = None
        for back in range(1, 160):
            j = ins[idx - back] if idx - back >= 0 else None
            if j is None:
                continue
            if j.address < f:
                break
            if j.mnemonic == 'addiu':
                a = addre.match(j.op_str)
                if a and a.group(1) == reg:
                    B = num(a.group(3))
                    if imm + B == target and B > 0:
                        kind = 'biased(+0x%x via %s)' % (B, j.op_str)
                    break
            elif j.mnemonic in ('lw', 'lui', 'move', 'addu', 'or') and j.op_str.startswith('$' + reg + ','):
                break
    if kind:
        print('%08x %-3s $%s, %s(%s)  [%s]  fn~%08x' % (i.address, i.mnemonic, m.group(1), m.group(2), '$' + reg, kind, f))
