"""Census of sprite headers by flag bits and program codes (alundra-project/Data/sprite-records.json).  python -B -I census_flags.py"""
import json
R='D:/development/repo/alundra-casaengine-project-converter/'
d=json.load(open(R+'alundra-project/Data/sprite-records.json'))
def flags(v): return v['MoreFlags'] | (v['CanPickup']<<8) | (v['FlagsPortraitShadowType']<<16)
rows=[]
for k,v in d.items():
    rows.append((v['BankKey'], flags(v), v))
def bit(f,b): return (f>>b)&1
print('total sprite records', len(rows))
for name,b in (('DestroyOnSlidingSlope',20),('DestroyOnVramFlags',21),('Gravity',8),('NoObstacleSlide',13),('DeactivateOnImpact',4),('Collidable',7)):
    ks=[r[0] for r in rows if bit(r[1],b)]
    print(name,'bit',b,'count',len(ks),'global:',[x for x in ks if x.startswith('alundra_')][:40], 'map-local count', len([x for x in ks if not x.startswith('alundra_')]))
# programs
def codes(v): return (v['ProgramLoad'],v['ProgramTick'],v['ProgramTouch'],v['ProgramDeactivate'],v['ProgramInteract'])
wanted={'E17':(3,17),'C94':(1,94),'C60':(1,60),'E9':(3,9),'C61':(1,61),'E10':(3,10)}
print()
for n,(slot,code) in (('E17',(3,17)),('C94',(1,94)),('C60',(1,60)),('E9',(3,9)),('C61',(1,61)),('E10',(3,10)),('E1',(3,1)),('E0',(3,0))):
    pass
names={'C94':('ProgramTick',94),'C60':('ProgramTick',60),'C61':('ProgramTick',61),'E17':('ProgramDeactivate',17),'E9':('ProgramDeactivate',9),'E10':('ProgramDeactivate',10),'E1':('ProgramDeactivate',1)}
for n,(f,c) in names.items():
    ks=sorted([r[0] for r in rows if r[2][f]==c], key=lambda s:(not s.startswith('alundra_'), int(s.split('_')[-1])))
    print(n, 'count', len(ks), ks[:30])
