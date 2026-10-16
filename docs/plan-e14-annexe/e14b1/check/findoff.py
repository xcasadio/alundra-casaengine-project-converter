import struct, exelib as X, sys
# find lw/sw/lbu/... with a given 16-bit offset
off=int(sys.argv[1],16)
ops={0x23:'lw',0x2b:'sw',0x24:'lbu',0x20:'lb',0x28:'sb',0x21:'lh',0x25:'lhu',0x29:'sh'}
code=X.DATA[0x800:0x800+X.T_SIZE]
n=len(code)//4
w=struct.unpack('<%dI'%n, code)
for i,x in enumerate(w):
    op=x>>26
    if op in ops and (x&0xffff)==off:
        print(hex(0x80020000+4*i), ops[op], 'rt=%d rs=%d'%((x>>16)&31,(x>>21)&31))
