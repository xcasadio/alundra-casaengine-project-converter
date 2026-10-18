import struct, sys
from c_exe import load
pc0, gp0, tbase, tsize, img = load()
targets = [int(x,0) for x in sys.argv[1:]]
words = struct.unpack("<%dI" % (len(img)//4), img[:len(img)//4*4])
for t in targets:
    print("== target %08X" % t)
    for i, w in enumerate(words):
        a = tbase + 4*i
        op = w >> 26
        if op in (2, 3) and (w & 0x3FFFFFF) == ((t >> 2) & 0x3FFFFFF):
            print("  %s at %08X" % ("jal" if op == 3 else "j  ", a))
        if w == t:
            print("  data word at %08X" % a)
