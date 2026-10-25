import sys
exec(open('bx.py').read().split("if __name__")[0])
import struct
offset=int(sys.argv[1],16)
ops=(0x23,0x25,0x24,0x21,0x20,0x2B,0x29,0x28)
for i in range(0, len(data)-3, 4):
    w = struct.unpack_from('<I', data, i)[0]
    a=0x80020000+i-0x800
    if a>=0x800CA800: break
    if (w>>26) in ops and (w & 0xFFFF)==offset:
        ins=list(md.disasm(struct.pack('<I',w), a))[0]
        print("%08x %s %s"%(a,ins.mnemonic,ins.op_str))
