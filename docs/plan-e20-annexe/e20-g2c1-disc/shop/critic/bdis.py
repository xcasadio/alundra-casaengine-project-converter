import os, sys, struct
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
sys.path.append('C:/Users/casad/AppData/Roaming/Python/Python314/site-packages')
import capstone, exelib
md = capstone.Cs(capstone.CS_ARCH_MIPS, capstone.CS_MODE_MIPS32 + capstone.CS_MODE_LITTLE_ENDIAN)
a = int(sys.argv[1], 16); b = int(sys.argv[2], 16)
for addr in range(a, b, 4):
    w = exelib.u32(addr)
    txt = ''
    for i in md.disasm(struct.pack('<I', w), addr):
        txt = '%-9s %s' % (i.mnemonic, i.op_str)
    print('0x%08x  %08x  %s' % (addr, w, txt))
