import struct, sys, mylib as m
def func_start(a):
    while a > m.TADDR:
        w=struct.unpack_from("<I",m.DATA,m.off(a))[0]
        if (w>>26)==9 and ((w>>21)&31)==29 and ((w>>16)&31)==29 and (w&0x8000):
            return a
        a-=4
    return None
for t in sys.argv[1:]:
    t=int(t,16); print(hex(t),'in func',hex(func_start(t)))
