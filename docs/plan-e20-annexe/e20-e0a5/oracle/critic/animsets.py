import sys,os
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)),'tools'))
import pworld as P
recs=P.global_records()
for i in (3,27,28,29,251,25,11):
    r=recs[i]
    print('sprite',i,'animsets',len(r['AnimSets']))
    for k,s in enumerate(r['AnimSets'][:6]):
        per=[]
        for dr in range(4):
            an=s['PreloadedAnims'][dr] if dr<len(s['PreloadedAnims']) else None
            if an is None: per.append(None); continue
            fr=an['Frames']; disp=[f for f in fr if not f['IsTransitionFrame']]; term=[f for f in fr if f['IsTransitionFrame']]
            per.append((len(disp),[f['Delay']&0x7F for f in disp][:4],('loop' if term and term[0]['Delay']==1 else ('end/chain%d'%term[0]['TransformIndexLow'] if term else '?'))))
        print('  set',k,'acc=%x'%s['Acceleration'],'sfx',s['Sfx'],per)
