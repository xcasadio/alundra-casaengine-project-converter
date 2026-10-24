"""scan_off.py - list every lw/sw/lbu/sb/lhu/sh with a given displacement in ALUN_CD.EXE text (read-only).
Usage: python -B scan_off.py HEXOFF [sw|lw|all]"""
import sys, capstone, exelib
off = int(sys.argv[1], 16)
mode = sys.argv[2] if len(sys.argv) > 2 else 'sw'
md = capstone.Cs(capstone.CS_ARCH_MIPS, capstone.CS_MODE_MIPS32 + capstone.CS_MODE_LITTLE_ENDIAN)
md.skipdata = True
code = exelib.DATA[exelib.off(exelib.T_ADDR):exelib.off(exelib.T_ADDR) + exelib.T_SIZE]
for i in md.disasm(code, exelib.T_ADDR):
    m = i.mnemonic
    if m in ('sw', 'sb', 'sh', 'lw', 'lbu', 'lb', 'lhu', 'lh'):
        if mode == 'sw' and not m.startswith('s'):
            continue
        if mode == 'lw' and not m.startswith('l'):
            continue
        ops = i.op_str.replace(' ', '')
        if '(' in ops:
            d = ops.split(',', 1)[1].split('(')[0]
            try:
                v = int(d, 0)
            except ValueError:
                continue
            if v == off:
                print('%#010x  %-5s %s' % (i.address, m, i.op_str))
