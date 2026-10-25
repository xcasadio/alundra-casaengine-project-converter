import sys
exec(open('bx.py').read().split("if __name__")[0])
import struct
# find sw/sh/sb with given offset
def scan(offset, ops=(0x2B,0x29,0x28)):
    res=[]
    for i in range(0, len(data)-3, 4):
        w = struct.unpack_from('<I', data, i)[0]
        if (w>>26) in ops and (w & 0xFFFF)==offset:
            a=0x80020000+i-0x800
            if 0x80020000<=a<0x800CA800:
                res.append((a,w))
    return res
off_ = int(sys.argv[1],16)
for a,w in scan(off_):
    ins=list(md.disasm(struct.pack('<I',w), a))[0]
    print("%08x %s %s"%(a,ins.mnemonic,ins.op_str))
