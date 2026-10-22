import csv, sys, collections
M32=0xFFFFFFFF
def s32(x):
    x&=M32
    return x-(1<<32) if x&0x80000000 else x
P=sys.argv[1]
rows=list(csv.DictReader(open(P,encoding='utf-8'),delimiter='\t'))
def parse(r):
    op=int(r['op'],16); instr=[int(x,16) for x in r['instr'].split()]
    pos=[int(r[k],16) for k in ('pos_x','pos_y','pos_z')]
    memo=[int(r[k],16) for k in ('memo_p2','memo_p3','memo_p4')]
    rin=int(r['result_in'],16)
    exp=(int(r['result_out'],16),tuple(int(r[k],16) for k in ('memo_p2_out','memo_p3_out','memo_p4_out')),int(r['v0']))
    return op,instr,pos,memo,rin,exp
def impl(op,instr,pos,memo,rin,mut=None):
    """returns (result, memo, size)"""
    if op==0x6A:
        m=list(pos)
        if mut=='6A_noZ': m[2]=memo[2]
        if mut=='6A_noY': m[1]=memo[1]
        r=rin
        if mut=='6A_res1': r=1
        if mut=='6A_res0': r=0
        if mut=='6A_noop': m=list(memo)
        return r&M32,tuple(x&M32 for x in m),1
    k=op-0x6B
    if mut=='owner_read': pass
    kk=k
    if mut=='6D_reads_P3' and op==0x6D: kk=1
    a=memo[kk]; b=pos[k]
    if mut=='swap': d=s32(b-a)
    elif mut=='sub64': d=a-b  # 64 bit, no wrap
    else: d=s32(a-b)
    if mut=='mathabs':
        if s32(d)==-(1<<31): raise OverflowError
        d=abs(d)
    elif mut=='noabs': pass
    elif mut=='sub64':
        d=abs(d)
    else:
        if d<0: d=s32(-d)
    lo,hi=instr[1],instr[2]
    thr=(lo|(hi<<8))<<16
    thr_s=s32(thr)
    if mut=='unsigned':
        d=d&M32; thr_s=thr&M32
    if mut=='pixels':
        thr_s=(lo|(hi<<8)); d=d>>16
    if mut=='lo_only': thr_s=s32(lo<<16)
    if mut=='be': thr_s=s32(((hi)|(lo<<8))<<16)
    if mut=='le_cmp': res=0 if d<=thr_s else 1
    elif mut=='inverted': res=1 if d<thr_s else 0
    else: res=0 if d<thr_s else 1
    return res&M32,tuple(x&M32 for x in memo),3
MUTS=['mathabs','pixels','unsigned','le_cmp','inverted','sub64','noabs','lo_only','be','6A_noZ','6A_noY','6A_res1','6A_res0','6D_reads_P3','swap']
for mut in [None]+MUTS:
    killed=0; per=collections.Counter(); exc=0
    for r in rows:
        op,instr,pos,memo,rin,exp=parse(r)
        try:
            got=impl(op,instr,pos,memo,rin,mut)
        except OverflowError:
            killed+=1; per[op]+=1; exc+=1; continue
        e=(exp[0],exp[1],exp[2])
        if op==0x6A:
            # for 6A the expected memo out = pos
            pass
        if got!=e:
            killed+=1; per[op]+=1
    print("%-12s killed rows %4d  (per op %s, exceptions %d)"%(mut,killed,dict(sorted(per.items())),exc))
# red today
red=collections.Counter(); tot=collections.Counter()
for r in rows:
    op,instr,pos,memo,rin,exp=parse(r)
    tot[op]+=1
    today=(rin&M32,tuple(x&M32 for x in memo),exp[2])  # skip by size: nothing changes
    if today!=exp: red[op]+=1
print("total",dict(tot)); print("red today",dict(red))
