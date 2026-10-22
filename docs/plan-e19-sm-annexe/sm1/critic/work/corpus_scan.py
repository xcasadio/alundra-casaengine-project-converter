"""Critic: independent census of 0x60 0x61 0x3C 0x3D over EVERY map of the export, by a linear sweep of each map's Codes
(DLL size table), checking that every program root lands on an instruction boundary."""
import re, json, os, glob, sys, collections
root=r"D:/development/repo/alundra-casaengine-project-converter"
s=open(root+'/Alundra/Events/EventOpcodeSizeTable.cs',encoding='utf-8-sig').read()
tab={int(a,16):int(b) for a,b,c in re.findall(r'\{\s*0x([0-9A-Fa-f]{2}),\s*new\((-?\d+),\s*"([^"]*)"',s)}
def size(op):
    z=tab.get(op,1)
    return z if z>0 else 1
TARGET={0x60,0x61,0x3C,0x3D}
maps={}
for p in glob.glob(root+'/alundra-project/Maps/**/*.events.json',recursive=True):
    d=json.load(open(p,encoding='utf-8-sig'))
    maps[d['MapIndex']]=(p,d)
print("maps in export:",len(maps))
sites=[]
bad_align=collections.Counter()
for mi,(p,d) in sorted(maps.items()):
    codes=d['Codes']
    roots={}
    for slot in 'ABCDEF':
        for i,off in enumerate(d['EventCodes%sTable'%slot]):
            roots.setdefault(off,[]).append('%s[%d]'%(slot,i))
    # linear sweep
    starts=set(); pc=0
    while pc<len(codes):
        starts.add(pc)
        pc+=size(codes[pc])
    mis=[(o,r) for o,r in roots.items() if o<len(codes) and o not in starts]
    if mis: bad_align[mi]=len(mis)
    pc=0
    while pc<len(codes):
        op=codes[pc]
        if op in TARGET:
            sites.append((mi,pc,op,tuple(codes[pc+1:pc+size(op)])))
        pc+=size(op)
print("maps with a program root off the sweep's instruction boundaries:",dict(bad_align))
cnt=collections.Counter((s[0],s[2]) for s in sites)
print("sites by opcode:",collections.Counter(s[2] for s in sites))
print("maps:",sorted({s[0] for s in sites}))
json.dump(sites,open('notes/linear_sweep_sites.json','w'))
