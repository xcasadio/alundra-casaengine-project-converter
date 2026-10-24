import sys, glob, re, json
sys.path.insert(0, 'D:/development/repo/alundra-casaengine-project-converter/docs/plan-e20-annexe/e20-g3/dll')
import decode_events as de
R='D:/development/repo/alundra-casaengine-project-converter'
TARGET={163,258,298,398,134}
files={}
for f in glob.glob(R+'/alundra-project/Maps/*/*/events/*.events.json'):
    f=f.replace(chr(92),'/'); m=re.search(r'-(\d+)\.events\.json$',f)
    if m: files[int(m.group(1))]=f
print('maps with events:',len(files))
remaps=[]; warps=[]
for m,f in sorted(files.items()):
    doc=json.load(open(f,encoding='utf-8'))
    for slot,i,s,e in de.all_programs(doc):
        for pc,op,v,bad in de.sweep(doc,s,e):
            if v is None: break
            if op==0x38:
                remaps.append((m,slot,i,pc,v[2]<<8|v[1],v[4]<<8|v[3]))
            if op==0x53:
                dest=v[1]|v[2]<<8
                warps.append((m,slot,i,pc,dest,v[3],v[4],v[5],v[6]))
print('--- 0x38 remaps (src->dst), all:')
for r in remaps: print(r)
print('--- 0x53 to targets (raw operand):')
for w in warps:
    if w[4] in TARGET or w[4] in {x[5] for x in remaps if x[4] in TARGET}: print(w)
print('total 0x53:',len(warps))
