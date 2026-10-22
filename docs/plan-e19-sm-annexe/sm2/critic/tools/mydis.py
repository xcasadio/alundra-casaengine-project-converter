import sys; sys.path.insert(0,'tools')
from mybin import *
from capstone import *
md = Cs(CS_ARCH_MIPS, CS_MODE_MIPS32|CS_MODE_LITTLE_ENDIAN)
md.detail=False
def dis(a,n):
    code = D[off(a):off(a)+4*n]
    for i in md.disasm(code,a):
        print(f"{i.address:08x}  {code[i.address-a:i.address-a+4][::-1].hex()}  {i.mnemonic:8s} {i.op_str}")
if __name__=='__main__':
    a=int(sys.argv[1],16); n=int(sys.argv[2]); dis(a,n)
