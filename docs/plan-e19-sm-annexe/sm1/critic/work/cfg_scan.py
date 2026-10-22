"""Critic: own control-flow census of 0x60 0x61 0x3C 0x3D on the 483 exported maps (branch model read from Dispatch; superset variant
that keeps falling through after unconditional jumps)."""
import re, json, glob, collections, csv
root=r"D:/development/repo/alundra-casaengine-project-converter"
s=open(root+'/Alundra/Events/EventOpcodeSizeTable.cs',encoding='utf-8-sig').read()
SZ={int(a,16):int(b) for a,b,c in re.findall(r'\{\s*0x([0-9A-Fa-f]{2}),\s*new\((-?\d+),\s*"([^"]*)"',s)}
TARGET={0x60,0x61,0x3C,0x3D}
def s16(c,i):
    v=(c[i] if i<len(c) else 0)|((c[i+1] if i+1<len(c) else 0)<<8)
    return v-65536 if v>=32768 else v
def walk(c,entries,superset):
    n=len(c); seen={}; work=list(entries); marks=set(); ret=False
    while True:
        while work:
            pc=work.pop()
            while 0<=pc<n and pc not in seen:
                op=c[pc]; seen[pc]=op
                z=SZ.get(op)
                if z is None or z==0 or op==0xFF:
                    break
                nxt=pc+z; stop=False
                if op==0x02:
                    if superset: work.append(pc+s16(c,pc+1))
                    else: nxt=pc+s16(c,pc+1)
                elif op in (3,4): work.append(pc+s16(c,pc+1))
                elif op in (0x30,0x31): work.append(pc+s16(c,pc+3))
                elif op==0x74: work.append(pc+s16(c,pc+1))
                elif op in (0x57,0x58):
                    for d in range(4): work.append(pc+s16(c,pc+1+2*d))
                    stop=not superset
                elif op==0x78:
                    marks.add(pc+3)
                    if superset: work.append(pc+s16(c,pc+1))
                    else: nxt=pc+s16(c,pc+1)
                elif op==0x79:
                    marks.add(pc+3); work.append(pc+s16(c,pc+1))
                elif op in (0x7B,0x7C):
                    marks.add(pc+5); work.append(pc+s16(c,pc+3))
                elif op==0x7D: ret=True; stop=not superset
                elif op in (0x7E,0x7F,0x80,0x81): ret=True
                elif op==0x49:
                    if superset: work.append(entries[0])
                    else: nxt=entries[0]
                elif op==0x4B: work.append(entries[0])
                if stop: break
                pc=nxt
        if ret:
            fresh=[m for m in marks if m not in seen]
            if fresh: work.extend(fresh); continue
        break
    return seen
res={False:set(),True:set()}
for p in glob.glob(root+'/alundra-project/Maps/**/*.events.json',recursive=True):
    d=json.load(open(p,encoding='utf-8-sig')); mi=d['MapIndex']; c=d['Codes']
    for slot in 'ABCDEF':
        for i,off in enumerate(d['EventCodes%sTable'%slot]):
            for sup in (False,True):
                seen=walk(c,[off],sup)
                for pc,op in seen.items():
                    if op in TARGET:
                        res[sup].add((mi,slot,i,pc,op,tuple(c[pc+1:pc+SZ[op]])))
for sup in (False,True):
    st=res[sup]
    print("superset" if sup else "strict", "program-sites:",len(st),"distinct (map,pc):",len({(a,d) for a,b,c_,d,e,f in st}),"by op:",collections.Counter(e for a,b,c_,d,e,f in st),"maps:",len({a for a,*_ in st}))
# compare with the annex tsv
rows=list(csv.DictReader(open(root+'/docs/plan-e19-sm-annexe/skipped-sites.tsv',encoding='utf-8'),delimiter='\t'))
ann={(int(r['map']),r['slot'],int(r['program']),int(r['pc']),int(r['opcode'],16),tuple(int(x,16) for x in r['operands'].split())) for r in rows if int(r['opcode'],16) in TARGET}
print("annex tsv rows for the four opcodes:",len(ann))
print("strict vs annex: only mine",len(res[False]-ann),"only annex",len(ann-res[False]))
print("superset minus strict (code only reachable past unconditional jumps):",len(res[True]-res[False]))
print(sorted(res[True]-res[False])[:20])
json.dump(sorted(map(list,res[False])),open('notes/cfg_strict_sites.json','w'))
