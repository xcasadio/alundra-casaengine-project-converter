import struct, mylib as L, bisect
n = len(L.TEXT)//4
W = [struct.unpack_from('<I', L.TEXT, 4*i)[0] for i in range(n)]
def s16(x): return x-0x10000 if x&0x8000 else x
# function starts: addiu sp,sp,-N
starts = [L.TBASE+4*i for i in range(n) if (W[i]>>16)==0x27BD and (W[i]&0x8000)]
def fn(a):
    k = bisect.bisect_right(starts, a)-1
    return starts[k] if k>=0 else 0
res = {}
for i in range(n):
    w = W[i]
    if w>>26 == 0x0F and (w&0xFFFF)==0x800A:
        rt = (w>>16)&31
        for j in range(i+1, min(n, i+14)):
            v = W[j]; op = v>>26
            if op in (0x23,0x2b) and ((v>>21)&31)==rt and s16(v&0xFFFF) == -0x78f8:
                res.setdefault(fn(L.TBASE+4*j), []).append((L.TBASE+4*j, 'lw' if op==0x23 else 'sw'))
            if op in (0x0F,0x09,0x23,0x0d) and ((v>>16)&31)==rt: break
for f,l in sorted(res.items()):
    print('func %08x: %s' % (f, ' '.join('%08x/%s'%x for x in l)))
