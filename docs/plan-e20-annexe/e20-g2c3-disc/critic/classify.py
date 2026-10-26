import sys, json, collections
sys.path.insert(0,'D:/development/repo/alundra-casaengine-project-converter/docs/plan-e20-annexe/e20-g3/dll')
import decode_events as de
R="D:/development/repo/alundra-casaengine-project-converter"
sig=collections.Counter(); waits=[]
rows=[]
for m in (87,88,90,92,94,158):
    doc=de.load_map(m)
    d=json.load(open(R+'/data-extracted/data/map_%d.json'%m,encoding='utf-8-sig'))
    ents=d['SpriteInfo']['Entities']['Entities']
    progs={ (slot,i):(s,e) for slot,i,s,e in de.all_programs(doc)}
    for k,e in enumerate(ents):
        if e is None or e['SpriteTableIndex']!=135: continue
        c=e['EventCodesC_TickIndex']
        if c==0: continue
        assert c&0x80, c
        idx=c&0x7f
        s,en=progs[('C',idx)]
        ops=[(op,tuple(v)) for pc,op,v,bad in de.sweep(doc,s,en)]
        # signature: opcode sequence
        seq=tuple(op for op,v in ops)
        sig[seq]+=1
        rows.append((m,k,idx,ops))
for s,c in sig.items(): print(c,[hex(x) for x in s])
print()
# print one example of each signature
seen=set()
for m,k,idx,ops in rows:
    s=tuple(op for op,v in ops)
    if s in seen: continue
    seen.add(s); print('map',m,'rec',k,'C[%d]'%idx,ops)
# wait values for the simple ones
simple=[ops for m,k,idx,ops in rows if tuple(op for op,v in ops)==(0x37,0x40,0xFF,0x00)]
print(len(simple), sorted(v[1] for ops in simple for op,v in ops if op==0x37))
simple=[ops for m,k,idx,ops in rows if tuple(op for op,v in ops)==(0x37,0x40,0xFF,0x00,0x00)]
ns=sorted(v[1] for ops in simple for op,v in ops if op==0x37)
print(len(simple), ns, collections.Counter(ns))
# flag-conditioned ones: print all
for m,k,idx,ops in rows:
    s=tuple(op for op,v in ops)
    if s[0]==0x30: print('map',m,'rec',k,'C[%d]'%idx,ops)
