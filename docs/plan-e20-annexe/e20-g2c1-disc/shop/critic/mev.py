import json,sys
R="D:/development/repo/alundra-casaengine-project-converter"
for m in sys.argv[1:]:
    d=json.load(open(R+'/data-extracted/data/map_%s.json'%m,encoding='utf-8-sig'))
    recs=d['SpriteInfo']['MapEvents']['Records']
    print('map',m,len(recs))
    for k,e in enumerate(recs):
        if isinstance(e,dict) and e.get('EventCodesBIndex',e.get('B',0)) or (isinstance(e,dict) and k<3):
            print(k,e)
