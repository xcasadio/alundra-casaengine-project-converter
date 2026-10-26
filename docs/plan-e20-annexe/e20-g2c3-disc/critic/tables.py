import sys
sys.path.insert(0, r'C:\Users\casad\AppData\Local\Temp\claude\D--development-repo-alundra-casaengine-project-converter\b00d1a72-420d-4acc-ab34-0c25fbdad3a1\scratchpad\t2-next\g2c3-critic')
from bx import *
# master table at 0x80098F4C
m = words(0x80098F4C, 8)
print('master', [hex(x) for x in m])
A=m[0]; C=m[2]
print('A', hex(A), 'C', hex(C))
print('A19 entry addr', hex(A+19*4), hex(u32(A+19*4)))
print('C71 entry addr', hex(C+71*4), hex(u32(C+71*4)))
# which codes in table A / C share the same handler
import collections
for name,base in (("A",m[0]),("C",m[2]),("E",m[4])):
    d=collections.defaultdict(list)
    for i in range(256):
        d[u32(base+4*i)].append(i)
    for k,v in d.items():
        if k in (0x800619D0,0x8007B834): print(name,hex(k),v)
