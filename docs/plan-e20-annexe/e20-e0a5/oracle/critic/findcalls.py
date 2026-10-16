import struct,sys
EXE = r"D:\development\repo\Alundra Remake\Alundra (France)\Alundra (France)_extracted\ALUN_CD.EXE"
D = open(EXE,'rb').read()
BASE=0x80020000; OFF=0x800
N=(len(D)-OFF)//4
def w(i): return struct.unpack_from('<I',D,OFF+4*i)[0]
targets={0x80039D04:'InitializeEntity',0x80038AB4:'UpdateAnimation',0x80039F58:'SpawnWarpEntity',0x8003A1B8:'RecordSpawner',0x8003A048:'SpawnChildEntity',0x8003ABCC:'RespawnInPlace',0x80031974:'HeroRebuild'}
for t,nm in targets.items():
    res=[]
    for i in range(N):
        x=w(i); pc=BASE+4*i
        op=x>>26
        if op in (2,3):
            tgt=(pc&0xF0000000)|((x&0x3FFFFFF)<<2)
            if tgt==t: res.append(('jal' if op==3 else 'j',pc))
    # lui/ori constant
    hi=t>>16; lo=t&0xFFFF
    consts=[]
    for i in range(N-1):
        x=w(i)
        if (x>>26)==0xF and (x&0xFFFF)==hi:
            for k in range(1,6):
                if i+k<N:
                    y=w(i+k)
                    if (y>>26) in (0xD,0x9) and (y&0xFFFF)==lo: consts.append(BASE+4*i)
    ptrs=[BASE+4*i for i in range(N) if w(i)==t]
    print(nm,hex(t),'calls',[(k,hex(p)) for k,p in res],'luiori',[hex(c) for c in consts],'dataptr',[hex(p) for p in ptrs][:10])
