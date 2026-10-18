import sys
from c_lst import dis, tbase, img
from c_exe import u32
def listing(a, n):
    for x in dis(a, n):
        print("%08X  %08x  %-8s %s" % (x.address, int.from_bytes(x.bytes,'little'), x.mnemonic, x.op_str))
if __name__ == "__main__":
    a = int(sys.argv[1], 0); n = int(sys.argv[2], 0)
    if len(sys.argv) > 3 and sys.argv[3] == "w":
        for i in range(n):
            print("%08X: %08X" % (a+4*i, u32(img, tbase, a+4*i)))
    elif len(sys.argv) > 3 and sys.argv[3] == "h":
        import struct
        for i in range(n):
            print("%08X: %6d  0x%04X" % (a+2*i, struct.unpack_from('<h', img, a-tbase+2*i)[0], struct.unpack_from('<H', img, a-tbase+2*i)[0]))
    else:
        listing(a, n)
