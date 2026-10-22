import struct,sys
sys.path.insert(0,'work')
from mydisasm import *
d_=d
# scan whole text for any instruction whose low16 == 0x8FAC
hits=[]
for a in range(0x80030000,0x800A0000,4):
    w_=struct.unpack('<I',rd(a,4))[0]
    if (w_&0xFFFF)==0x8FAC and (w_>>26) in (0x23,0x09,0x24,0x21):
        hits.append(a)
print([hex(h) for h in hits])
for h in hits:
    print('--- around',hex(h))
    dis(h-0x30,0xa0)
