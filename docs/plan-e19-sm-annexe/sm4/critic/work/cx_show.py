import sys, cx_corpus as C
mid=int(sys.argv[1]); slot=sys.argv[2]; prog=int(sys.argv[3]); n=int(sys.argv[4]) if len(sys.argv)>4 else 60
b,d,f=C.load(mid)
progs=C.programs(d)
e=[x for (s,i,x) in progs if s==slot and i==prog][0]
codes=d['Codes']
entries=sorted({x for (_,_,x) in progs})
print(mid,b,slot,prog,'entry',e,'next entries',[x for x in entries if x>e][:2],'len',len(codes))
pc=e
for k in range(n):
    if pc>=len(codes): break
    op=codes[pc]; sz=C.SZ.get(op,1) or 1
    print('%6d  %02X %-38s %s'%(pc,op,C.NM.get(op,'?'),' '.join('%02X'%x for x in codes[pc+1:pc+sz])))
    pc+=sz
