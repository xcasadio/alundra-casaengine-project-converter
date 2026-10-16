import exelib
from fractions import Fraction
T=0x80028E0C
for r in (0x7F,0x7D,0x7E):
    print(hex(r), [hex(exelib.u8(T+r*16+i)) for i in range(16)])
from collections import Counter
def dist(code):
    c=Counter()
    def rec(code,p,steps):
        if code>=0x100: c[(0,steps)]+=p; return
        if not code&0x80:
            c[(code if code<0x62 else 0, steps)]+=p; return
        for i in range(16):
            rec(exelib.u8(T+(code&0x7f)*16+i), p/16, steps+1)
    rec(code,Fraction(1),0)
    return c
c=dist(0xFF)
tot=Counter()
for (k,s),p in c.items(): tot[k]+=p
for k,p in sorted(tot.items()): print(hex(k), p*256)
for (k,s),p in sorted(c.items()): print(hex(k), s, p*256)
print("seed init", hex(exelib.u32(0x80098708)))
