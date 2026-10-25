import sys
sys.path.insert(0, r'C:\Users\casad\Documents\x')
exec(open('bx.py').read().split("if __name__")[0])
import struct
print("exe size", len(data), hex(len(data)))
# header
print("hdr", data[:8], [hex(x) for x in struct.unpack_from('<8I', data, 0x10)])
tgt = 0x80031A68
jal = 0x0C000000 | ((tgt & 0x0FFFFFFF) >> 2)
j = 0x08000000 | ((tgt & 0x0FFFFFFF) >> 2)
print("jal word %08x j word %08x" % (jal, j))
for i in range(0, len(data)-3, 4):
    w = struct.unpack_from('<I', data, i)[0]
    a = 0x80020000 + i - 0x800
    if w == jal: print("JAL at %08x (file off %x)" % (a, i))
    if w == j: print("J at %08x" % a)
    if w == tgt: print("WORD ptr at %08x" % a)
# unaligned scan for pointer
for i in range(0, len(data)-3):
    if i%4==0: continue
    w = struct.unpack_from('<I', data, i)[0]
    if w == tgt: print("unaligned ptr at file off %x" % i)
# lui/addiu or lui/ori materialization of 0x80031A68
lui = []
for i in range(0, len(data)-8, 4):
    w = struct.unpack_from('<I', data, i)[0]
    if (w>>26)==0xF and (w & 0xFFFF)==0x8003:
        rt=(w>>16)&31
        for k in range(1,12):
            w2 = struct.unpack_from('<I', data, i+4*k)[0]
            op=w2>>26
            if op in (9,13) and ((w2>>21)&31)==rt and (w2&0xFFFF)==0x1A68:
                print("lui/addiu|ori 0x80031A68 at %08x +%d" % (0x80020000+i-0x800, k))
            if op==9 and ((w2>>21)&31)==rt and (w2&0xFFFF)==0x1A68: pass
