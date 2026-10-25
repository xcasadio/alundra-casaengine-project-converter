import json, sys
d=json.load(open('D:/development/repo/alundra-casaengine-project-converter/data-extracted/data/map_alundra.json'))
sr = d['SpriteInfo']['SpriteRecords'][0]
for st in (0x00, 0x01, 0x1C, 0x1D, 0x31, 0x39, 0x3A, 0x3B, 0x3C, 0x3E, 0x4E):
    a = sr['AnimSets'][st]
    print("anim 0x%02x: Speed=%s Flags(=AnimFlags src)=%s Sfx=%s Accel=%s IsZForce=%s offsets=%s" % (st, a['Speed'], a['Flags'], a['Sfx'], a['Acceleration'], a['IsZForceApplied'], a['AnimationOffsets']))
    for di, pa in enumerate(a['PreloadedAnims']):
        fr = pa['Frames']
        print("   dir%d nframes=%d delays(raw)=%s" % (di, pa['NumberOfFrames'], [f['Delay'] for f in fr]))
        if di >= 1: break
