"""scan_field.py OFFSET [OFFSET ...] [--lo 0x8002E000] [--hi 0x80090000]
Read-only scan of ALUN_CD.EXE (France): every load/store with the immediate OFFSET (hex), with the nearest preceding function prologue
(addiu $sp,$sp,-N) and the base register. Run: python -B -I -X utf8 scan_field.py 0x18C"""
import os, re, struct, sys
sys.path.insert(0, r"C:\Users\casad\AppData\Roaming\Python\Python314\site-packages")
import capstone
EXE = r"D:\development\repo\Alundra Remake\Alundra (France)\Alundra (France)_extracted\ALUN_CD.EXE"
DATA = open(EXE, "rb").read()
BASE, OFF = 0x80010000, 0x800
# PS-X EXE header: text address at 0x18
T_ADDR = struct.unpack_from('<I', DATA, 0x18)[0]
BASE = T_ADDR
md = capstone.Cs(capstone.CS_ARCH_MIPS, capstone.CS_MODE_MIPS32 + capstone.CS_MODE_LITTLE_ENDIAN)
args = [a for a in sys.argv[1:] if not a.startswith('--')]
lo, hi = 0x8002E000, 0x80090000
if '--lo' in sys.argv: lo = int(sys.argv[sys.argv.index('--lo') + 1], 16)
if '--hi' in sys.argv: hi = int(sys.argv[sys.argv.index('--hi') + 1], 16)
offs = {int(a, 16) for a in args if a.startswith('0x')}
N = (len(DATA) - OFF) // 4
ins = []
for k in range(0, N, 8192):
    n = min(8192, N - k)
    chunk = DATA[OFF + 4 * k: OFF + 4 * (k + n)]
    got = {i.address: i for i in md.disasm(chunk, BASE + 4 * k)}
    for j in range(n):
        a = BASE + 4 * (k + j)
        ins.append(got.get(a) or None)
pro = re.compile(r'^\$sp, \$sp, -(0x[0-9a-f]+|\d+)$')
starts = []
for i in ins:
    if i is not None and i.mnemonic == 'addiu' and pro.match(i.op_str):
        starts.append(i.address)
import bisect
pat = re.compile(r'^\$(\w+), (-?0x[0-9a-f]+|-?\d+)\(\$(\w+)\)$')
for i in ins:
    if i is None or i.mnemonic not in ('lw', 'sw', 'lh', 'lhu', 'sh', 'lb', 'lbu', 'sb'):
        continue
    if not (lo <= i.address < hi):
        continue
    m = pat.match(i.op_str)
    if not m:
        continue
    v = int(m.group(2), 16) if 'x' in m.group(2) else int(m.group(2))
    if v in offs:
        f = starts[bisect.bisect_right(starts, i.address) - 1]
        print('%08x %-3s $%s, 0x%x($%s)   fn~%08x' % (i.address, i.mnemonic, m.group(1), v, m.group(3), f))
