import struct, mylib as L
n = len(L.TEXT)//4
W = [struct.unpack_from('<I', L.TEXT, 4*i)[0] for i in range(n)]
def s16(x): return x-0x10000 if x&0x8000 else x
found = []
for i in range(n):
    w = W[i]
    if w>>26 == 0x0F and (w&0xFFFF) in (0x8013,):
        rt = (w>>16)&31
        for j in range(i+1, min(n, i+12)):
            v = W[j]
            op = v>>26
            if op in (0x20,0x21,0x23,0x24,0x25,0x28,0x29,0x2b,0x09,0x0d) and ((v>>21)&31)==rt:
                imm = s16(v&0xFFFF)
                if 0x4368 <= imm < 0x4368+0x294:
                    found.append((L.TBASE+4*j, op, imm))
            # stop when rt is overwritten
            if op in (0x0F,0x09,0x23,0x0d,0x24,0x25,0x20,0x21) and ((v>>16)&31)==rt and j>i: break
names = {0x20:'lb',0x21:'lh',0x23:'lw',0x24:'lbu',0x25:'lhu',0x28:'sb',0x29:'sh',0x2b:'sw',0x09:'addiu',0x0d:'ori'}
for a,op,imm in found: print('%08x %s imm=%#x -> addr %#x' % (a, names[op], imm, 0x80130000+imm))
