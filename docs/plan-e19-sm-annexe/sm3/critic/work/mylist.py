import sys, mylib as m
T=0x80098FAC
ops=[0x9E,0xA9,0xAA,0xB2,0xB4,0xB5,0xB6,0xB7]
allh=sorted(set(m.u32(T+4*i) for i in range(0,0xC0)))
for op in ops:
    a=m.u32(T+4*op)
    nxt=[h for h in allh if h>a][0]
    print("=== op %02X handler %08X .. %08X (%d insns)"%(op,a,nxt,(nxt-a)//4))
    for i in m.dis(a,(nxt-a)//4):
        print("%08X  %-8s %s"%(i.address,i.mnemonic,i.op_str))
