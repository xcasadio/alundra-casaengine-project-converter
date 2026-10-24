"""scan_imm.py IMM... : every instruction whose operand text contains the immediate (e.g. 0x7d2c) with the nearest prologue. Read-only."""
import bisect, re, struct, sys
sys.path.insert(0, r"C:\Users\casad\AppData\Roaming\Python\Python314\site-packages")
import capstone
EXE = r"D:\development\repo\Alundra Remake\Alundra (France)\Alundra (France)_extracted\ALUN_CD.EXE"
DATA = open(EXE, "rb").read()
BASE = struct.unpack_from('<I', DATA, 0x18)[0]
md = capstone.Cs(capstone.CS_ARCH_MIPS, capstone.CS_MODE_MIPS32 + capstone.CS_MODE_LITTLE_ENDIAN)
want = [a.lower() for a in sys.argv[1:]]
N = (len(DATA) - 0x800) // 4
ins = []
for k in range(0, N, 8192):
    n = min(8192, N - k)
    ins += list(md.disasm(DATA[0x800 + 4 * k: 0x800 + 4 * (k + n)], BASE + 4 * k))
starts = [i.address for i in ins if i.mnemonic == 'addiu' and i.op_str.startswith('$sp, $sp, -')]
for i in ins:
    if any(re.search(r'(^|[ ,(])' + re.escape(w) + r'(\)|$|,)', i.op_str) for w in want):
        f = starts[bisect.bisect_right(starts, i.address) - 1]
        print('%08x %-6s %-28s fn~%08x' % (i.address, i.mnemonic, i.op_str, f))
