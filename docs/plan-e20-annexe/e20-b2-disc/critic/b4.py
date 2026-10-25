import sys
sys.path.insert(0,'.')
from lib import *
N=0xAA800//4
W=[u32(BASE+4*i) for i in range(N)]
idx=[i for i,x in enumerate(W) if x==0x8003FE7C]
print('refs to 0x8003FE7C:',[hex(BASE+4*i) for i in idx])
for i in idx:
    tb=BASE+4*(i-0x82)
    print('table base',hex(tb))
    for op in (3,4,0x82,0xC2):
        print(hex(op), hex(W[(tb-BASE)//4+op]))
