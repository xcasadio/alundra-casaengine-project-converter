import sys
sys.path.insert(0, r"C:/Users/casad/AppData/Roaming/Python/Python314/site-packages")
import os
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(HERE, 'g1a-work'))
import capstone, exelib
md = capstone.Cs(capstone.CS_ARCH_MIPS, capstone.CS_MODE_MIPS32 + capstone.CS_MODE_LITTLE_ENDIAN)
a = int(sys.argv[1], 16); b = int(sys.argv[2], 16)
code = exelib.DATA[exelib.off(a):exelib.off(a) + (b - a)]
for ins in md.disasm(code, a):
    print(f"{ins.address:#010x}  {ins.mnemonic:8s} {ins.op_str}")
