# independent scan: any load whose effective address resolves to the pad struct 0x80126F18..0x80126F2F within a function range
import sys, lib, re
lo=int(sys.argv[1],16); hi=int(sys.argv[2],16)
ins=[i for i in lib.all_ins() if lo<=i.address<hi]
regs={}
for i in ins:
    m=i.mnemonic; ops=[o.strip() for o in i.op_str.split(',')]
    if m=='lui': regs[ops[0]]=int(ops[1],16)<<16; continue
    if m=='addiu' and ops[1] in regs and ops[0]!=ops[1]:
        regs[ops[0]]=(regs[ops[1]]+int(ops[2],16 if '0x' in ops[2] else 10))&0xffffffff; continue
    if m=='addiu' and ops[1] in regs and ops[0]==ops[1]:
        regs[ops[0]]=(regs[ops[1]]+int(ops[2],0))&0xffffffff; continue
    mm=re.match(r'(-?0x[0-9a-f]+|-?\d+)\((\$\w+)\)', ops[-1]) if len(ops)>1 else None
    if m in('lhu','lh','lw','lbu','lb') and mm and mm.group(2) in regs:
        ea=(regs[mm.group(2)]+int(mm.group(1),0))&0xffffffff
        if 0x80126f18<=ea<0x80126f30: print("%08x %s %s -> %08x"%(i.address,m,i.op_str,ea))
