import capstone, exelib
md = capstone.Cs(capstone.CS_ARCH_MIPS, capstone.CS_MODE_MIPS32 + capstone.CS_MODE_LITTLE_ENDIAN)
md.skipdata = True
code = exelib.DATA[exelib.off(exelib.T_ADDR):exelib.off(exelib.T_ADDR) + exelib.T_SIZE]
with open('lst/all.txt', 'w') as f:
    for i in md.disasm(code, exelib.T_ADDR):
        f.write('%#010x  %-8s %s\n' % (i.address, i.mnemonic, i.op_str))
