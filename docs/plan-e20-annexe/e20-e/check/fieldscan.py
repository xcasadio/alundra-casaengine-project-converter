import struct, sys
import exelib
D=exelib.DATA; base=exelib.T_ADDR
n=(len(D)-0x800)//4
offs={int(a,16) for a in sys.argv[1:]}
names={0x2b:'sw',0x23:'lw',0x29:'sh',0x21:'lh',0x25:'lhu',0x28:'sb',0x20:'lb',0x24:'lbu'}
for i in range(n):
    w=struct.unpack_from("<I",D,0x800+4*i)[0]
    op=w>>26
    if op in names:
        imm=w&0xffff
        if imm in offs:
            print("%#x %s r%d, %#x(r%d)"%(base+4*i,names[op],(w>>16)&31,imm,(w>>21)&31))
