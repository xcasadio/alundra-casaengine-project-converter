import csv, sys, collections
M32=0xFFFFFFFF
def s32(x):
    x&=M32
    return x-(1<<32) if x&0x80000000 else x
rows=list(csv.DictReader(open(sys.argv[1],encoding='utf-8'),delimiter='\t'))
def parse(r):
    op=int(r['op'],16); instr=[int(x,16) for x in r['instr'].split()]
    pos=[s32(int(r[k],16)) for k in ('pos_x','pos_y','pos_z')]
    memo=[s32(int(r[k],16)) for k in ('memo_p2','memo_p3','memo_p4')]
    rin=int(r['result_in'],16)
    exp=int(r['result_out'],16)
    return op,instr,pos,memo,rin,exp
def f(op,instr,pos,memo,mut):
    k=op-0x6B
    mk=k; pk=k
    if mut=='memo_axis_next': mk=(k+1)%3
    if mut=='memo_axis_prev': mk=(k+2)%3
    if mut=='pos_axis_next': pk=(k+1)%3
    if mut=='pos_axis_prev': pk=(k+2)%3
    d=s32(memo[mk]-pos[pk])
    if d<0: d=s32(-d)
    lo,hi=instr[1],instr[2]
    if mut=='signed_lo': lo_v = lo-256 if lo>=128 else lo
    else: lo_v=lo
    if mut=='signed_hi': hi_v=hi-256 if hi>=128 else hi
    else: hi_v=hi
    sh=16
    if mut=='shift15': sh=15
    if mut=='shift8': sh=8
    if mut=='shift24': sh=24
    if mut=='hi_shift9': thr=s32((lo_v|(hi_v<<9))<<16)
    else: thr=s32((lo_v|(hi_v<<8))<<sh)
    if mut=='thr_unsigned_only':  # compare as long: distance signed, thr unsigned 32
        thr=((lo_v|(hi_v<<8))<<16)&M32
    if mut=='dist_unsigned_only':
        d=d&M32; thr=s32(thr)
    if mut=='thr_long':  # 64-bit threshold without wrap
        thr=((lo_v|(hi_v<<8))<<16)
    if mut=='neg_wrap_clamp':  # abs clamps MinValue to MaxValue
        pass
    return 0 if d<thr else 1
def f2(op,instr,pos,memo,mut):
    if mut=='abs_clamp':
        k=op-0x6B
        d=s32(memo[k]-pos[k])
        if d<0: d= 0x7FFFFFFF if d==-(1<<31) else -d
        thr=s32(((instr[1]|(instr[2]<<8))<<16))
        return 0 if d<thr else 1
    return f(op,instr,pos,memo,mut)
MUTS=['memo_axis_next','memo_axis_prev','pos_axis_next','pos_axis_prev','signed_lo','signed_hi','shift15','shift8','shift24','hi_shift9','thr_unsigned_only','dist_unsigned_only','thr_long','abs_clamp']
for mut in MUTS:
    killed=0; per=collections.Counter()
    for r in rows:
        op,instr,pos,memo,rin,exp=parse(r)
        if op==0x6A: continue
        if f2(op,instr,pos,memo,mut)!=exp: killed+=1; per[op]+=1
    print("%-20s killed %4d %s"%(mut,killed,dict(sorted(per.items()))))
# coverage stats
c=collections.Counter()
for r in rows:
    op,instr,pos,memo,rin,exp=parse(r)
    if op==0x6A: continue
    k=op-0x6B; d=s32(memo[k]-pos[k]); dd=abs(d) if d!=-(1<<31) else d
    thr=s32(((instr[1]|(instr[2]<<8))<<16))
    if instr[1]>=0x80 and instr[2]<0x80: c['lo>=80,hi<80']+=1
    if instr[1]>=0x80 and instr[2]>=0x80: c['lo>=80,hi>=80']+=1
    if instr[2]>=0x80: c['hi>=80']+=1
    if d==thr and thr>0: c['d==thr>0']+=1
    if dd==thr-1 and thr>0: c['d==thr-1']+=1
    if dd==thr+1 and thr>0: c['d==thr+1']+=1
    if d==-(1<<31): c['d==MinValue']+=1
    if s32(memo[k]-pos[k])!=memo[k]-pos[k]: c['wraps']+=1
    if instr[1]==0 and instr[2]==0: c['thr==0']+=1
    if thr==-(1<<31): c['thr==MinValue']+=1
    if d<0 and d!=-(1<<31): c['d<0']+=1
    if d==0: c['d==0']+=1
print(dict(c))
