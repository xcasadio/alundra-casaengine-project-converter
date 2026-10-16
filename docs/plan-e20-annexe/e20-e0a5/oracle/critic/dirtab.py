import struct
EXE = r"D:\development\repo\Alundra Remake\Alundra (France)\Alundra (France)_extracted\ALUN_CD.EXE"
D = open(EXE,'rb').read()
BASE=0x80020000; OFF=0x800
def u32(a): return struct.unpack_from('<I',D,a-BASE+OFF)[0]
T=0x800237F4
for r in range(4):
    print('row',r,[u32(T+32*r+4*k) for k in range(8)])
print('dir word -> index (d+2)&0x1c:')
for d in (0,8,0x10,0x18,4,0xC,0x14,0x1C):
    idx=((d+2)&0x1c)//4
    print(hex(d),'idx',idx,'by old row:',[u32(T+32*r+4*idx) for r in range(4)])
