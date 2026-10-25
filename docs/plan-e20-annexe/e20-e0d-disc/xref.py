"""Find lui/ls data references and jal call sites in ALUN_CD.EXE text. Usage: xref.py data 0x80127108 | xref.py call 0x80045004"""
import sys
sys.path.insert(0, r'C:/Users/casad/AppData/Local/Temp/claude/D--development-repo-alundra-casaengine-project-converter/b00d1a72-420d-4acc-ab34-0c25fbdad3a1/scratchpad/t2-chest')
import bx, bisect
TEXT0=0x80020000; TEXT1=0x800CA800
N=(TEXT1-TEXT0)//4
W=[bx.u32(TEXT0+4*i) for i in range(N)]
FUNCS=[TEXT0+4*i for i in range(N) if (W[i]>>16)==0x27BD and (W[i]&0x8000) and (W[i]&0xFFFF)!=0]
def func_of(a):
    i=bisect.bisect_right(FUNCS,a)-1
    return FUNCS[i] if i>=0 else None
def data_refs(addr, window=40):
    hi=(addr+0x8000)>>16 & 0xFFFF; lo=addr & 0xFFFF
    if lo>=0x8000: pass
    out=[]
    for i in range(N):
        w=W[i]
        if (w>>26)==15 and (w&0xFFFF)==hi:  # lui
            rt=(w>>16)&31
            for j in range(i+1,min(i+window,N)):
                v=W[j]
                op=v>>26
                rs=(v>>21)&31
                imm=v&0xFFFF
                if op in (32,33,35,36,37,40,41,43) and rs==rt:  # lb lh lw lbu lhu sb sh sw
                    simm=imm-0x10000 if imm&0x8000 else imm
                    if ((hi<<16)+simm)&0xFFFFFFFF==addr:
                        names={32:'lb',33:'lh',35:'lw',36:'lbu',37:'lhu',40:'sb',41:'sh',43:'sw'}
                        out.append((TEXT0+4*j,names[op]))
                if op in (9,13) and rs==rt and ((v>>16)&31)==rt:
                    pass
                # stop if rt overwritten by another lui
                if (v>>26)==15 and ((v>>16)&31)==rt: break
    return out
def calls(target):
    out=[]
    for i in range(N):
        w=W[i]
        if (w>>26)==3 and ((((w&0x03FFFFFF)<<2)|0x80000000)==target):
            out.append(TEXT0+4*i)
    return out
if __name__=='__main__':
    kind=sys.argv[1]; t=int(sys.argv[2],16)
    if kind=='data':
        for a,k in data_refs(t): print("%08x %s  in func %08x" % (a,k,func_of(a) or 0))
    else:
        for a in calls(t): print("%08x  in func %08x" % (a,func_of(a) or 0))
