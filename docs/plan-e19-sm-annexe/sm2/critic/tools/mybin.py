import struct, sys
EXE = r"D:\development\repo\Alundra Remake\Alundra (France)\Alundra (France)_extracted\ALUN_CD.EXE"
D = open(EXE,'rb').read()
assert D[:8]==b'PS-X EXE'
pc0,gp0,taddr,tsize = struct.unpack_from('<IIII',D,0x10)
def off(a): return 0x800 + (a-taddr)
def u32(a): return struct.unpack_from('<I',D,off(a))[0]
def u16(a): return struct.unpack_from('<H',D,off(a))[0]
def u8(a): return D[off(a)]
if __name__=='__main__':
    print(hex(pc0),hex(gp0),hex(taddr),hex(tsize),len(D))
    # what is at 0x80098F4C..0x80098FC0
    for a in range(0x80098F4C,0x80098FC0,4):
        print(hex(a),hex(u32(a)))
