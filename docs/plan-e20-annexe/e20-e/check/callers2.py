import struct, sys
import exelib
D=exelib.DATA
base=exelib.T_ADDR
n=(len(D)-0x800)//4
print(hex(base), hex(exelib.T_SIZE), len(D))
targets=[int(a,16) for a in sys.argv[1:]]
for t in targets:
    print("== target", hex(t))
    for i in range(n):
        w=struct.unpack_from("<I",D,0x800+4*i)[0]
        a=base+4*i
        op=w>>26
        if op in (2,3) and (w&0x3ffffff)==((t>>2)&0x3ffffff):
            print("  %s at %#x  (word %#x)" % ("jal" if op==3 else "j", a, w))
        if w==t:
            print("  data word == target at %#x" % a)
        # lui/addiu or lui/ori pair
        if op==0xF:
            rt=(w>>16)&31
            hi=w&0xffff
            for k in range(1,4):
                if i+k>=n: break
                w2=struct.unpack_from("<I",D,0x800+4*(i+k))[0]
                op2=w2>>26
                if op2 in (9,0xD,0x23) and ((w2>>21)&31)==rt:
                    imm=w2&0xffff
                    if op2==9 or op2==0x23:
                        simm = imm-0x10000 if imm&0x8000 else imm
                        val=((hi<<16)+simm)&0xffffffff
                    else:
                        val=(hi<<16)|imm
                    if val==t:
                        print("  lui/%s pair at %#x" % (hex(op2), a))
