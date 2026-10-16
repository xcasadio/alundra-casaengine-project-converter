import sys, capstone, exelib as X
md = capstone.Cs(capstone.CS_ARCH_MIPS, capstone.CS_MODE_MIPS32 + capstone.CS_MODE_LITTLE_ENDIAN)
md.detail=False
def dump(start, nwords, out=sys.stdout):
    code = X.DATA[X.off(start):X.off(start)+4*nwords]
    ins=list(md.disasm(code,start))
    tg=set()
    for i in ins:
        if i.mnemonic in ('beq','bne','blez','bgtz','bltz','bgez','b','beqz','bnez','bal','bltzal','bgezal'):
            try: tg.add(int(i.op_str.split(',')[-1].strip(),16))
            except: pass
    for i in ins:
        mark = '>' if i.address in tg else ' '
        print(f"{mark}{i.address:#010x}  {i.mnemonic:7s} {i.op_str}", file=out)
if __name__=='__main__':
    dump(int(sys.argv[1],16), int(sys.argv[2],0))
