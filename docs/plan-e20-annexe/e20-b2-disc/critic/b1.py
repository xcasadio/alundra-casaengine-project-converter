import sys
sys.path.insert(0,'.')
from lib import *
T=0x800B9FE8
for i in range(0x80):
    a=T+10*i
    slot,flag,rank,mx,icon=s16(a),u16(a+2),s16(a+4),s16(a+6),s16(a+8)
    if slot in (1,2,3,4,5,6):
        print(i,'slot',slot,'flag',flag,'rank',rank,'max',mx,'icon',icon)
