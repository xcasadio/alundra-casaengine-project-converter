import struct, exelib as X, sys
code=X.DATA[0x800:0x800+X.T_SIZE]
n=len(code)//4
w=struct.unpack('<%dI'%n, code)
for tgt in sys.argv[1:]:
    t=int(tgt,16)
    for i,x in enumerate(w):
        if (x>>26)==3 and ((x&0x3ffffff)<<2)==(t&0x0fffffff):
            print(tgt,'called from',hex(0x80020000+4*i))
