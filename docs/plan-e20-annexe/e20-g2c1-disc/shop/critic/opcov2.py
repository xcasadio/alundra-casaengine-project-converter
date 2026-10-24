import sys, re, collections
sys.path.insert(0, 'D:/development/repo/alundra-casaengine-project-converter/docs/plan-e20-annexe/e20-g3/dll')
import decode_events as de
src=open('D:/development/repo/alundra-casaengine-project-converter/Alundra/Events/EventProgramRunner.cs',encoding='utf-8-sig').read()
cases=set(int(x,16) for x in re.findall(r'case 0x([0-9A-Fa-f]{2})\s*:',src))
for a,b in re.findall(r'case >= 0x([0-9A-Fa-f]{2}) and <= 0x([0-9A-Fa-f]{2})',src):
    cases|=set(range(int(a,16),int(b,16)+1))
for m in (134,163,258,298,398):
    doc=de.load_map(m); used=collections.defaultdict(list)
    for slot,i,s,e in de.all_programs(doc):
        if i==0: continue
        for pc,op,v,bad in de.sweep(doc,s,e):
            if v is None: break
            if op not in cases and op!=0xFF: used[op].append('%s[%d]@%d'%(slot,i,pc))
    print(m,{('0x%02X'%k+' '+de.SIZES.get(k,(0,'?'))[1]):(len(v),v[:6]) for k,v in sorted(used.items())})
