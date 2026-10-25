import sys
exec(open('bx.py').read().split("if __name__")[0])
import struct
vals=[int(x,16) for x in sys.argv[1:]]
for i in range(0, len(data)-3, 4):
    w = struct.unpack_from('<I', data, i)[0]
    a=0x80020000+i-0x800
    if a>=0x800CA800: break
    if (w>>26) in (9,13,12) and (w & 0xFFFF) in vals:
        ins=list(md.disasm(struct.pack('<I',w), a))[0]
        print("%08x %s %s"%(a,ins.mnemonic,ins.op_str))
