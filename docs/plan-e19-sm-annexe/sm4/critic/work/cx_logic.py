import csv, collections
import cx_corpus as C
rows=list(csv.DictReader(open(C.REPO+'/docs/plan-e19-sm-annexe/skipped-sites.tsv',encoding='utf-8'),delimiter='\t'))
sel=[r for r in rows if r['opcode'] in ('96','88','72','97','B3')]
res=collections.Counter()
for r in sel:
    mid=int(r['map']); slot=r['slot']; prog=int(r['program']); pc=int(r['pc']); op=r['opcode']
    b,d,f=C.load(mid)
    progs=C.programs(d)
    e=[x for (s,i,x) in progs if s==slot and i==prog][0]
    entries=sorted({x for (_,_,x) in progs})
    codes=d['Codes']
    nxt=[x for x in entries if x>e]; end=nxt[0] if nxt else len(codes)
    seq=C.sweep(codes,e,end)
    before=[(p,o,ops) for (p,o,ops) in seq if p<pc]
    ctx=[(p,'%02X'%o,ops) for (p,o,ops) in before if o in (0x42,0x43,0x66)]
    key=(op,r['operands'],slot,bool(ctx))
    res[key]+=1
    if op=='96' and r['operands'].startswith('80') and slot!='B' :
        print(mid,b,slot,prog,pc,r['operands'],'ctx',ctx[-2:])
for k,v in sorted(res.items()): print(k,v)
