import struct
exec(open('bx.py').read().split("if __name__")[0])
for val in (0x31,0x3a,0x1c):
    print("== constant", hex(val))
    for i in range(0, len(data)-3, 4):
        w = struct.unpack_from('<I', data, i)[0]
        a=0x80020000+i-0x800
        if a>=0x800CA800: break
        if (w>>26)==9 and ((w>>21)&31)==0 and (w&0xFFFF)==val:
            rt=(w>>16)&31
            # look ahead 6 instrs for sw rt,0x88(*)
            for k in range(1,7):
                w2=struct.unpack_from('<I', data, i+4*k)[0]
                if (w2>>26)==0x2B and ((w2>>16)&31)==rt and (w2&0xFFFF)==0x88:
                    print("  %08x li -> sw 0x88 at %08x"%(a, a+4*k))
