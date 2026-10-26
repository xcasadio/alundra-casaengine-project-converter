import json
R="D:/development/repo/alundra-casaengine-project-converter/data-extracted/data/"
g=json.load(open(R+'map_alundra.json',encoding='utf-8-sig'))
recs=g['SpriteInfo']['SpriteRecords']
for sid in (243,241,242):
    for ai,a in enumerate(recs[sid]['AnimSets']):
        print('anim',ai,{k:a[k] for k in ('IsZForceApplied','Speed','Unknown','Flags','Sfx','Acceleration')}, 'nPre',len(a['PreloadedAnims']))
        pa=a['PreloadedAnims'][0]
        print('  dir0 frames',pa['NumberOfFrames'], 'total delay', sum(f['Delay'] for f in pa['Frames']))
        for fi,f in enumerate(pa['Frames']):
            cd=f['CollisionData']
            if cd: print('   frame',fi,'delay',f['Delay'],'coll',cd)
