import sys; sys.path.insert(0,'tools')
from mybin import *
targets=[0x8003F8DC,0x8003F908,0x8003F94C,0x8003F990]
# scan whole image for pointer arrays containing those
for t in targets:
    hits=[]
    for a in range(taddr, taddr+tsize-3, 4):
        if u32(a)==t: hits.append(a)
    print(hex(t),[hex(h) for h in hits])
