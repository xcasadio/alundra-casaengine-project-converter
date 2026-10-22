import sys, csv
sys.path.insert(0,'tools')
M32=0xFFFFFFFF
def s32(x):
    x&=M32; return x-(1<<32) if x&0x80000000 else x
P="C:/Users/casad/AppData/Local/Temp/claude/D--development-repo-alundra-casaengine-project-converter/b00d1a72-420d-4acc-ab34-0c25fbdad3a1/scratchpad/sm-prep/sm2/vectors/vectors-sm2.tsv"
rows=list(csv.DictReader(open(P),delimiter='\t'))
h=lambda x:int(x,16)
def thr_of(lo,hi): return s32(((lo|(hi<<8))<<16))
def correct(memo,pos,lo,hi):
    d=s32(memo-pos)
    if d<0: d=s32(-d)
    return 0 if d<thr_of(lo,hi) else 1
def mutants(memo,pos,lo,hi):
    d0=s32(memo-pos); thr=thr_of(lo,hi)
    out={}
    # Math.Abs
    out['Math.Abs']= 'EXC' if d0==-(1<<31) else (0 if abs(d0)<thr else 1)
    d=d0
    if d<0: d=s32(-d)
    out['pixels']= 0 if (d>>16)<(lo|(hi<<8)) else 1
    out['unsigned']= 0 if (d&M32)<(thr&M32) else 1
    out['<=']= 0 if d<=thr else 1
    out['invert']= 1-correct(memo,pos,lo,hi)
    out['long']= 0 if abs(s32(memo)-s32(pos))<thr else 1
    out['noabs']= 0 if d0<thr else 1
    out['swap']= (lambda dd: 0 if (s32(-dd) if dd<0 else dd)<thr else 1)(s32(pos-memo))
    out['sbyte']= 0 if d<s32((lo|(((hi-256) if hi>=128 else hi)<<8))<<16) else 1
    out['be']= 0 if d<s32(((hi|(lo<<8))<<16)) else 1
    out['lo only']= 0 if d<s32(lo<<16) else 1
    return out
kill={}
for r in rows:
    op=int(r['op'],16)
    if op==0x6A: continue
    k=op-0x6B
    instr=[int(b,16) for b in r['instr'].split()]
    memo=h(r['memo_p2'])  # placeholder
    memos=(h(r['memo_p2']),h(r['memo_p3']),h(r['memo_p4'])); poss=(h(r['pos_x']),h(r['pos_y']),h(r['pos_z']))
    exp=h(r['result_out'])
    m=mutants(memos[k],poss[k],instr[1],instr[2])
    for name,val in m.items():
        if val!=exp: kill[name]=kill.get(name,0)+1
print(kill)
