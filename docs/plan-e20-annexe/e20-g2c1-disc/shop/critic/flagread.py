import sys, glob, re, json
sys.path.insert(0, 'D:/development/repo/alundra-casaengine-project-converter/docs/plan-e20-annexe/e20-g3/dll')
import decode_events as de
R='D:/development/repo/alundra-casaengine-project-converter'
CHAIN={389,390,476,478,392,391,416,163,162,165,164,172,169,170,171,173,174,175,10,179,176,177,178,180,181,182,135,183,184,185}
IDS={708:'vessel258/298',1009:'vest398',1108:'vessel398'}
FL={0x05,0x06,0x32,0x30,0x31,0x35,0x36}
for f in sorted(glob.glob(R+'/alundra-project/Maps/*/*/events/*.events.json')):
    f=f.replace(chr(92),'/'); m=int(re.search(r'-(\d+)\.events\.json$',f).group(1))
    doc=json.load(open(f,encoding='utf-8'))
    for slot,i,s,e in de.all_programs(doc):
        for pc,op,v,bad in de.sweep(doc,s,e):
            if v is None: break
            hit=None
            if op in FL: hit=v[1]|v[2]<<8
            if op in (0x33,0x34):
                ids=[v[k]|v[k+1]<<8 for k in range(1,len(v)-1,2)]
                for x in ids:
                    if x in IDS: print(m,'CHAIN' if m in CHAIN else '', '%s[%d]@%d'%(slot,i,pc),hex(op),x,IDS[x])
            if hit in IDS and not (m in (258,298,398) and slot=='B'):
                print(m,'CHAIN' if m in CHAIN else '', '%s[%d]@%d'%(slot,i,pc),hex(op),hit,IDS[hit])
