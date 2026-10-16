import struct, exelib, sys
D=exelib.DATA; base=exelib.T_ADDR
n=(len(D)-0x800)//4
def W(a): return struct.unpack_from("<I",D,0x800+(a-base))[0]
def is_prologue(w): return (w>>16)==0x27bd and (w&0x8000)!=0
def fend(start):
    a=start+4
    # scan until next prologue after having seen a jr ra
    seen_ret=False
    while a<base+4*n:
        w=W(a)
        if w==0x03e00008: seen_ret=True
        if seen_ret and is_prologue(W(a+8)) and a+8>start+8:
            return a+8
        if is_prologue(w) and a>start+0x40 and W(a-4)==0 and W(a-8)==0x03e00008:
            return a
        a+=4
    return a
def callees(start):
    end=fend(start)
    out=set(); rng=False
    a=start
    while a<end:
        w=W(a)
        if w>>26==3: out.add((w&0x3ffffff)<<2|0x80000000)
        if (w>>26) in (0x23,0x2b) and (w&0xffff)==0x10000-0x78f8: rng=True
        a+=4
    return out,rng,end
seen={}
todo=[int(x,16) for x in sys.argv[1:]]
while todo:
    f=todo.pop()
    if f in seen: continue
    try:
        c,r,e=callees(f)
    except Exception as ex:
        seen[f]=(None,None,None); continue
    seen[f]=(c,r,e)
    todo.extend(c)
print(len(seen),"functions reached")
for f,(c,r,e) in sorted(seen.items()):
    if r: print("RNG access in", hex(f), "end", hex(e) if e else None)
