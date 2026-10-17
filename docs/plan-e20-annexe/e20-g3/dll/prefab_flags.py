import json,sys
d=json.load(open('D:/development/repo/alundra-casaengine-project-converter/alundra-project/Data/sprite-records.json',encoding='utf-8'))
keys=sys.argv[1:]
for g,h in d.items():
    if h.get('BankKey') in keys:
        fl=h['MoreFlags']|(h['CanPickup']<<8)|(h['FlagsPortraitShadowType']<<16)
        print(h['BankKey'], g, 'Flags=0x%X'%fl, 'Collidable' if fl&0x80 else 'not-collidable', 'kind=%d'%((fl&0x600)>>9), 'size', h['SizeX'],h['SizeY'],h['SizeZ'])
