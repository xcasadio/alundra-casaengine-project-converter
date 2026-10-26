"""sprite_anims.py MAP SPRITE - prints the animation sets of a sprite record of a map (flags, speed, frame count, delays, collision boxes). Run: python -B -I -X utf8 sprite_anims.py 86 137"""
import json, sys
R = r"D:/development/repo/alundra-casaengine-project-converter"
m, s = int(sys.argv[1]), int(sys.argv[2])
d = json.load(open(R + '/data-extracted/data/map_%d.json' % m, encoding='utf-8-sig'))
r = d['SpriteInfo']['SpriteRecords'][s]
print('Header', {k: v for k, v in r['Header'].items() if k not in ('Ubuff',)})
print('keys', [k for k in r.keys()])
for k, a in enumerate(r['AnimSets']):
    print('AnimSet %d: Speed=%s Flags=%s Sfx=%s Accel=%s IsZForceApplied=%s offsets=%s' % (k, a['Speed'], a['Flags'], a['Sfx'], a['Acceleration'], a['IsZForceApplied'], a['AnimationOffsets']))
    for j, an in enumerate(a['PreloadedAnims']):
        fr = an['Frames']
        print('   dir %d: %d frames, delays %s' % (j, an['NumberOfFrames'], [f['Delay'] for f in fr][:12]))
        if j == 0:
            for f in fr[:12]:
                print('      frame delay=%s coll=%s' % (f['Delay'], f['CollisionData']))
