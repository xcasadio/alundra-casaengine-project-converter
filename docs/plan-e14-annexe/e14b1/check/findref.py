import struct, exelib as X, capstone, sys
md = capstone.Cs(capstone.CS_ARCH_MIPS, capstone.CS_MODE_MIPS32 + capstone.CS_MODE_LITTLE_ENDIAN)
base=0x80020000
end=base+X.T_SIZE
target=int(sys.argv[1],16)
hi=((target+0x8000)>>16)&0xffff; lo=target&0xffff
if lo>=0x8000: lo-=0x10000
# scan words: lui with imm hi, then within 12 words an instruction with imm16 == lo (as signed)
code=X.DATA[0x800:0x800+X.T_SIZE]
n=len(code)//4
w=struct.unpack('<%dI'%n, code)
res=[]
for i,x in enumerate(w):
    if (x>>26)==0x0f and (x&0xffff)==hi:
        reg=(x>>16)&31
        for j in range(i+1,min(i+14,n)):
            y=w[j]
            op=y>>26
            if op in (0x08,0x09,0x23,0x21,0x25,0x20,0x24,0x2b,0x29,0x28) and ((y>>21)&31)==reg and ((y&0xffff)==(lo&0xffff)):
                res.append(base+4*i)
                break
for a in res: print(hex(a))
