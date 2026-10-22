import struct, sys, mylib as m
def callers(target):
    out=[]
    base=m.TADDR
    n=m.TSIZE//4
    for i in range(n):
        w=struct.unpack_from("<I",m.DATA,0x800+4*i)[0]
        if (w>>26)==3 and ((w&0x3FFFFFF)<<2)|0x80000000==target:
            out.append(base+4*i)
    return out
for t in sys.argv[1:]:
    t=int(t,16)
    print(hex(t), [hex(x) for x in callers(t)])
