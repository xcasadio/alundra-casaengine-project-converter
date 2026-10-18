"""usage: chk_disp.py OFFSET_HEX  -> every lw/sw/lh/sh/lb/sb with that displacement not on $sp"""
import re, sys, capstone, exelib
md = capstone.Cs(capstone.CS_ARCH_MIPS, capstone.CS_MODE_MIPS32 + capstone.CS_MODE_LITTLE_ENDIAN)
want = int(sys.argv[1], 16)
base = exelib.T_ADDR; data = exelib.DATA
n = (len(data) - 0x800) // 4
for i in range(n):
    addr = base + 4 * i
    r = list(md.disasm(data[0x800 + 4 * i:0x800 + 4 * i + 4], addr))
    if not r: continue
    x = r[0]
    mm = re.match(r'(\$\w+), (-?0x[0-9a-fA-F]+|-?\d+)\((\$\w+)\)', x.op_str)
    if mm and x.mnemonic in ('lw','sw','lh','lhu','sh','lb','lbu','sb') and int(mm.group(2),0)==want and mm.group(3)!='$sp':
        print("%08X %s %s" % (addr, x.mnemonic, x.op_str))
