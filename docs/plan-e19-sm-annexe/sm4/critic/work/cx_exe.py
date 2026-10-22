"""Critic's own loader + capstone listing (written from scratch). Read-only on the EXE."""
import struct, hashlib, sys
EXE = r"D:\development\repo\Alundra Remake\Alundra (France)\Alundra (France)_extracted\ALUN_CD.EXE"
D = open(EXE, "rb").read()
assert D[:8] == b"PS-X EXE"
PC0, GP0, TADDR, TSIZE = struct.unpack_from("<IIII", D, 0x10)
def off(a):
    o = 0x800 + (a - TADDR)
    assert 0x800 <= o < len(D), hex(a)
    return o
def u32(a): return struct.unpack_from("<I", D, off(a))[0]
def u8(a): return D[off(a)]
def s16(a): return struct.unpack_from("<h", D, off(a))[0]
def u16(a): return struct.unpack_from("<H", D, off(a))[0]
TABLE = 0x80098FAC
def handler(op): return u32(TABLE + 4*op)
if __name__ == "__main__":
    print("sha1", hashlib.sha1(D).hexdigest(), "pc0=%x gp=%x taddr=%x tsize=%x" % (PC0, GP0, TADDR, TSIZE))
