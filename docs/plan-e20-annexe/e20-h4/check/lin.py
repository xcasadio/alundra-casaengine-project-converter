import sys, struct, capstone, exelib
md = capstone.Cs(capstone.CS_ARCH_MIPS, capstone.CS_MODE_MIPS32 + capstone.CS_MODE_LITTLE_ENDIAN)
def dis(a, b):
    for addr in range(a, b, 4):
        w = exelib.u32(addr)
        ins = list(md.disasm(struct.pack('<I', w), addr))
        s = f"{ins[0].mnemonic} {ins[0].op_str}" if ins else f".word {w:08x}"
        print(f"{addr:08X}: {w:08X}  {s}")
if __name__ == '__main__':
    a = int(sys.argv[1], 16); b = int(sys.argv[2], 16)
    dis(a, b)
