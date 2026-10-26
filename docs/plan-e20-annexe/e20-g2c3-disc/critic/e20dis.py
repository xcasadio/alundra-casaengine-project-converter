import sys
sys.path.insert(0, r'C:\Users\casad\AppData\Local\Temp\claude\D--development-repo-alundra-casaengine-project-converter\b00d1a72-420d-4acc-ab34-0c25fbdad3a1\scratchpad\t2-next\g2c3-critic')
from bx import *
m=words(0x80098F4C,8)
E=m[4]; D=m[3]; Cc=m[2]; F=m[5]
for nm,b,c in (('E',E,20),('C',Cc,70),('D',D,0),('F',F,0)):
    print(nm,c,hex(u32(b+4*c)))
