import json, collections
R="D:/development/repo/alundra-casaengine-project-converter"
tot=collections.Counter()
for n in (87,88,90,92,94,158):
    d=json.load(open(R+'/data-extracted/data/map_%d.json'%n,encoding='utf-8-sig'))
    si=d['SpriteInfo']
    print('map',n)
    for k,e in enumerate(si['Entities']['Entities']):
        if e is None or e['SpriteTableIndex']!=135: continue
        ab=[e['EventCodes%s'%x] for x in ('A_LoadIndex','B_MapIndex','C_TickIndex','D_TouchIndex','E_DeactivateIndex','F_InteractIndex')]
        born = (e['SpriteDirection']&0x40)!=0
        print('  rec %3d dir=%3d born_at_load_bit=%d en=%d box x[%d..%d] y[%d..%d] tile=(%d,%d,h%d) A..F=%s contents=%d'%(k,e['SpriteDirection'],born,e['IsEnabled'],e['XMin'],e['XMax'],e['YMin'],e['YMax'],e['XPos'],e['YPos'],e['Height'],ab,e['Contents']))
        tot[(n,born, ab[2]!=0)]+=1
print(dict(tot))
