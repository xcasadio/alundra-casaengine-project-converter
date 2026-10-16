import sys,os
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)),'tools'))
import pworld as P
recs=P.global_records()
out=[]
for i,r in enumerate(recs):
    if not r or not r.get('AnimSets'): continue
    s=r['AnimSets'][0]
    for dr in range(4):
        an=s['PreloadedAnims'][dr] if dr<len(s['PreloadedAnims']) else None
        if an is None: continue
        disp=[f for f in an['Frames'] if not f['IsTransitionFrame']]
        if len(disp)>=2 and (disp[0]['Delay']&0x7F)==1:
            c0=disp[0].get('CollisionData'); c1=disp[1].get('CollisionData')
            if c0 and c1 and c0!=c1:
                out.append((i,dr,len(disp),str(c0)[:80],str(c1)[:80]))
                break
print(len(out))
for o in out[:12]: print(o)
