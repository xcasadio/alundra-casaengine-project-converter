import sys
sys.path.insert(0,'.')
from lib import *
tab=[u8(0x80028C14+i) for i in range(100)]
print('item->record nonzero:',{i:v for i,v in enumerate(tab) if v})
recs=[[u8(0x80028C78+13*r+k) for k in range(13)] for r in range(12)]
for r,x in enumerate(recs): print(r,' '.join('%02x'%b for b in x))
print('rec0==rec11', recs[0]==recs[11])
print('byte before table 0x80028C13:', u8(0x80028C13))
# byte0 of record = chargeable? ; animation addr: record*13 + 0x80028C79 (+4 air)
print('rec11 ground anim byte', '%02x'%u8(0x80028C79+13*11), 'air', '%02x'%u8(0x80028C79+13*11+4))
