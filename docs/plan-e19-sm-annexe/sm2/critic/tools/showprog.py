import sys; sys.path.insert(0,'tools')
from mycorpus import *
m=int(sys.argv[1]); s=sys.argv[2]; i=int(sys.argv[3])
ev,tm,b=load(m); codes=ev['Codes']; e=table(ev,s)[i]
seen=walk_program(codes,e,set())
print(b,s,i,'entry',e,'len',len(codes))
pcs=sorted(seen)
for pc in pcs:
    op=codes[pc]; sz=SIZE.get(op,1) or 1
    print('%5d  %02X %-40s %s'%(pc,op,NAME.get(op,'?'),' '.join('%02X'%c for c in codes[pc+1:pc+sz])))
