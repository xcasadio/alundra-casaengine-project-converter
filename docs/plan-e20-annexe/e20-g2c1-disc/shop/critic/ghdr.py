import json,sys
R="D:/development/repo/alundra-casaengine-project-converter"
d=json.load(open(R+'/data-extracted/data/map_alundra.json',encoding='utf-8-sig'))
recs=d['SpriteInfo']['SpriteRecords']
tab=d['SpriteInfo']['SpriteTable']
print(tab[66] if len(tab)>66 else None)
for t in (48,50,66,67,68,69,71,81,83,85,113):
    r=recs[t]
    h=r['Header']
    print(t, {k:h[k] for k in h if k in ('ProgramLoad','ProgramTick','ProgramTouch','ProgramDeactivate','ProgramInteract','MoreFlags','CanPickup','FlagsPortraitShadowType','BreakEffect')}, {k:r[k] for k in r if k!='Header'} if t==66 else '')
