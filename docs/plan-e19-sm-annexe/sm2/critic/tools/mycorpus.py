"""Own corpus access + own walker (from scratch, following the C# WalkProgram of AlundraStoryChainOpcodeAudit)."""
import json, os, re, glob
REPO='D:/development/repo/alundra-casaengine-project-converter'
PROJ=REPO+'/alundra-project'
IDX=json.load(open(PROJ+'/Maps/world-index.json',encoding='utf-8'))
src=open(REPO+'/Alundra/Events/EventOpcodeSizeTable.cs',encoding='utf-8').read()
SIZE={}; NAME={}
for m in re.finditer(r'\{\s*0x([0-9A-Fa-f]{2}),\s*new\((\d+),\s*"([^"]*)"\)\s*\}',src):
    SIZE[int(m.group(1),16)]=int(m.group(2)); NAME[int(m.group(1),16)]=m.group(3)
SL='ABCDEF'
def folder_base(mid):
    rel=IDX[str(mid)].replace(chr(92),'/')
    return PROJ+'/'+os.path.dirname(rel), os.path.splitext(os.path.basename(rel))[0]
def load(mid):
    f,b=folder_base(mid)
    ev=json.load(open(f+'/events/'+b+'.events.json',encoding='utf-8'))
    tm=glob.glob(f+'/tilemap/*.tileMap')
    assert len(tm)==1,(mid,tm)
    return ev,json.load(open(tm[0],encoding='utf-8')),b
def table(ev,slot): return ev['EventCodes%sTable'%slot]
def s16(v): v&=0xFFFF; return v-0x10000 if v&0x8000 else v
def roots(ev,tm):
    r=[]
    def add(slot,raw):
        raw=int(raw)
        idx=raw&0x7F
        t=table(ev,slot)
        if idx!=0 and idx<len(t) and t[idx]!=0: r.append((slot,idx))
    for layer in tm['object_layers']:
        if layer['name']=='MapEvents':
            for o in layer['objects']: add('B',o['custom_properties']['EventCodesBIndex'])
    for layer in tm['object_layers']:
        if layer['name']=='Entities':
            for o in layer['objects']:
                p=o['custom_properties']
                add('A',p['EventCodesA_LoadIndex']); add('C',p['EventCodesC_TickIndex']); add('D',p['EventCodesD_TouchIndex']); add('E',p['EventCodesE_DeactivateIndex']); add('F',p['EventCodesF_InteractIndex'])
    return r
def walk_program(codes,entry,extra):
    n=len(codes); seen={}; marks=set(); retusers=set(); work=[entry]
    B=lambda i: codes[i] if 0<=i<n else 0
    S=lambda lo,hi: s16(B(lo)|(B(hi)<<8))
    while True:
        while work:
            pc=work.pop()
            while 0<=pc<n and pc not in seen:
                op=codes[pc]; seen[pc]=op
                if op not in SIZE or SIZE[op]==0 or op==0xFF: break
                size=SIZE[op]; jumped=False; stop=False
                if op==0x02: pc+=S(pc+1,pc+2); jumped=True
                elif op in (3,4): work.append(pc+S(pc+1,pc+2))
                elif op in (0x30,0x31): work.append(pc+S(pc+3,pc+4))
                elif op==0x74: work.append(pc+S(pc+1,pc+2))
                elif op in (0x57,0x58):
                    for d in range(4): work.append(pc+S(pc+1+2*d,pc+2+2*d))
                    stop=True
                elif op==0x78: marks.add(pc+3); pc+=S(pc+1,pc+2); jumped=True
                elif op==0x79: marks.add(pc+3); work.append(pc+S(pc+1,pc+2))
                elif op in (0x7B,0x7C): marks.add(pc+5); work.append(pc+S(pc+3,pc+4))
                elif op==0x7D: retusers.add(pc); stop=True
                elif op in (0x7E,0x7F,0x80,0x81): retusers.add(pc)
                elif op==0x49: pc=entry; jumped=True
                elif op==0x4B: work.append(entry)
                elif op==0x40:
                    if 2<=B(pc+1)<=5: extra.add((SL[B(pc+1)],B(pc+2)&0x7F))
                if stop: break
                if not jumped: pc+=size
        if retusers:
            fresh=[m for m in marks if m not in seen]
            if fresh:
                work.extend(fresh); continue
        break
    return seen
def walk_map(ev,tm):
    codes=ev['Codes']; out=[]; done=set(); todo=list(roots(ev,tm))
    while todo:
        slot,idx=todo.pop(0)
        if (slot,idx) in done: continue
        done.add((slot,idx))
        extra=set()
        seen=walk_program(codes,table(ev,slot)[idx],extra)
        for pc in sorted(seen): out.append((slot,idx,pc,seen[pc]))
        for (s2,i2) in sorted(extra):
            t=table(ev,s2)
            if (s2,i2) not in done and i2<len(t) and t[i2]!=0: todo.append((s2,i2))
    return out
