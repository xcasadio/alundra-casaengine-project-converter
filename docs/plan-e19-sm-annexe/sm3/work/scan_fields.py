"""Read-only scan of ALUN_CD.EXE (France): every store (sw/sh/sb) and load to a given struct offset, with the enclosing function start
(nearest previous 'addiu $sp, $sp, -N' prologue).  Usage: python -B scan_fields.py OFFSET_HEX [store|load|both]"""
import sys, re, os
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', 'tools', 'check'))
import exelib, capstone
md = capstone.Cs(capstone.CS_ARCH_MIPS, capstone.CS_MODE_MIPS32 + capstone.CS_MODE_LITTLE_ENDIAN)
LO, HI = 0x80020000, exelib.T_ADDR + exelib.T_SIZE
code = exelib.DATA[exelib.off(LO):exelib.off(LO) + (HI - LO)]
class _I:
    __slots__=('address','mnemonic','op_str')
    def __init__(s,a,m,o): s.address=a; s.mnemonic=m; s.op_str=o
ins=[]
for a in range(LO, HI, 4):
    r = list(md.disasm(exelib.DATA[exelib.off(a):exelib.off(a)+4], a))
    ins.append(r[0] if r else _I(a,'.word',''))
byaddr = {i.address: k for k, i in enumerate(ins)}
def func_start(k):
    while k > 0:
        i = ins[k]
        if i.mnemonic == 'addiu' and i.op_str.startswith('$sp, $sp, -'):
            return i.address
        k -= 1
    return None
off = int(sys.argv[1], 16)
mode = sys.argv[2] if len(sys.argv) > 2 else 'store'
pat = re.compile(r'(-?0x[0-9a-f]+|-?\d+)\((\$\w+)\)$')
stores = ('sw', 'sh', 'sb'); loads = ('lw', 'lh', 'lhu', 'lb', 'lbu')
for k, i in enumerate(ins):
    m = pat.search(i.op_str)
    if not m:
        continue
    try:
        v = int(m.group(1), 16) if 'x' in m.group(1) else int(m.group(1))
    except ValueError:
        continue
    if v != off:
        continue
    isst = i.mnemonic in stores; isld = i.mnemonic in loads
    if (mode == 'store' and isst) or (mode == 'load' and isld) or (mode == 'both' and (isst or isld)):
        fs = func_start(k)
        print('%#010x  %-4s %-28s func %s' % (i.address, i.mnemonic, i.op_str, hex(fs) if fs else '?'))
