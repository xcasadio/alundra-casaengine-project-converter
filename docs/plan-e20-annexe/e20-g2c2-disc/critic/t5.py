import sys
sys.path.insert(0, '.')
from bx import *
T = {'A': 0x800C4B34, 'C': 0x800C4F34, 'D': 0x800C5334, 'E': 0x800C5734}
for s, c, exp in (('C',70,0x8007B7B0),('E',20,0x8007F6C8),('D',34,0x8007E8AC),('A',19,0x800619D0),('C',71,0x8007B834),('C',23,0x8006B848),('E',2,0x8007ED30),('C',4,0x80066984),('E',0,0x8007ED10),('C',60,0x8007A8A0),('E',9,0x8007EF50)):
    a = T[s] + 4*c
    print(s, c, hex(u32(a)), 'OK' if u32(a)==exp else 'MISMATCH expected %x' % exp)
# Are there other table entries pointing to the same handlers (aliases)?
for s, base in T.items():
    for c in range(256):
        v = u32(base + 4*c)
        if v in (0x8007B7B0, 0x8007F6C8, 0x8007E8AC, 0x800619D0, 0x8007B834) and (s,c) not in (('C',70),('E',20),('D',34),('A',19),('C',71)):
            print('alias', s, c, hex(v))
