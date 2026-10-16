import exelib
base=0x800228AC
from collections import defaultdict
d=defaultdict(list)
for i in range(0x48):
    t=exelib.u32(base+4*i)
    d[t].append(hex(i+4))
for t,v in sorted(d.items()):
    print(hex(t), v)
print("-> 0x8002fdbc:", d[0x8002fdbc])
