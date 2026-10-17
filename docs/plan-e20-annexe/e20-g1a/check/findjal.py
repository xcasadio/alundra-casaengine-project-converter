import sys, struct, exelib, capstone
md = capstone.Cs(capstone.CS_ARCH_MIPS, capstone.CS_MODE_MIPS32 + capstone.CS_MODE_LITTLE_ENDIAN)
targets = [int(t,16) for t in sys.argv[1:]]
D = exelib.DATA
base = exelib.T_ADDR
n = (len(D)-0x800)//4
for i in range(n):
    w = struct.unpack_from("<I", D, 0x800+4*i)[0]
    op = w>>26
    if op in (2,3):
        tgt = ((base+4*i+4) & 0xF0000000) | ((w & 0x3FFFFFF)<<2)
        if tgt in targets:
            print(hex(base+4*i), 'jal' if op==3 else 'j', hex(tgt))
