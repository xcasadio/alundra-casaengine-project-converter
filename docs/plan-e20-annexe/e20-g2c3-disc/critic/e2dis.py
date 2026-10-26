import sys
sys.path.insert(0, r'C:\Users\casad\AppData\Local\Temp\claude\D--development-repo-alundra-casaengine-project-converter\b00d1a72-420d-4acc-ab34-0c25fbdad3a1\scratchpad\t2-next\g2c3-critic')
from bx import *
m=words(0x80098F4C,8)
E=m[4]
print('E2 ',hex(u32(E+4*2)),'E20',hex(u32(E+4*20)))
hits=[]
n=(len(data)-0x800)//4
for i in range(n):
    a=0x80020000+4*i; w=u32(a)
    if (w>>26) in (0x09,0x0b,0x0d) and (w&0xFFFF) in (0x189,0x18a,0x18b):
        hits.append((a,w&0xFFFF))
print([(hex(h),hex(v)) for h,v in hits])
