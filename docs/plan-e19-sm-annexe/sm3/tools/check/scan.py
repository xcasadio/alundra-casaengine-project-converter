import exelib, capstone, sys, re
md = capstone.Cs(capstone.CS_ARCH_MIPS, capstone.CS_MODE_MIPS32 + capstone.CS_MODE_LITTLE_ENDIAN)
def allinsn(lo=0x80020000, hi=None):
    hi = hi or (exelib.T_ADDR+exelib.T_SIZE)
    for a in range(lo,hi,4):
        code=exelib.DATA[exelib.off(a):exelib.off(a)+4]
        for i in md.disasm(code,a):
            yield i
if __name__=='__main__':
    pat=re.compile(sys.argv[1]); mn=sys.argv[2].split(',') if len(sys.argv)>2 else None
    for i in allinsn():
        if (mn is None or i.mnemonic in mn) and pat.search(i.op_str):
            print(hex(i.address),i.mnemonic,i.op_str)
