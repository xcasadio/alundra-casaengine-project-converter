import struct, hashlib, sys
from capstone import *
EXE = r"D:\development\repo\Alundra Remake\Alundra (France)\Alundra (France)_extracted\ALUN_CD.EXE"
d = open(EXE,'rb').read()
print("sha1", hashlib.sha1(d).hexdigest(), "size", len(d))
pc0, gp0, taddr, tsize = struct.unpack_from("<IIII", d, 0x10)
print("pc0 %08x gp %08x taddr %08x tsize %x" % (pc0,gp0,taddr,tsize))
def off(a): return 0x800 + (a - taddr)
def u32(a): return struct.unpack_from("<I", d, off(a))[0]
tab = 0x80098FAC
md = Cs(CS_ARCH_MIPS, CS_MODE_MIPS32 + CS_MODE_LITTLE_ENDIAN)
md.detail = False
for op in (0x69,0x6A,0x6B,0x6C,0x6D,0x6E):
    h = u32(tab + 4*op)
    print("== op %02X handler %08x" % (op, h))
    code = d[off(h):off(h)+0x60]
    n=0
    for i in md.disasm(code, h):
        print("  %08x  %s %s" % (i.address, i.mnemonic, i.op_str))
        n+=1
        # stop after jr ra + delay slot
        if i.mnemonic == 'jr' and i.op_str=='$ra':
            nxt = list(md.disasm(d[off(i.address+4):off(i.address+8)], i.address+4))[0]
            print("  %08x  %s %s" % (nxt.address, nxt.mnemonic, nxt.op_str))
            break
