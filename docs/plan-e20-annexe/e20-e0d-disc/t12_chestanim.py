import json, io, sys
sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding='utf-8')
d = json.load(open('D:/development/repo/alundra-casaengine-project-converter/data-extracted/data/map_alundra.json', encoding='utf-8'))
r = d['SpriteInfo']['SpriteRecords'][30]
h = r['Header']
print('header:', {k: h[k] for k in ('MoreFlags', 'CanPickup', 'FlagsPortraitShadowType', 'ProgramLoad', 'ProgramTick', 'ProgramTouch', 'ProgramDeactivate', 'ProgramInteract', 'OffsetX', 'OffsetY', 'OffsetZ', 'SizeX', 'SizeY', 'SizeZ', 'BreakEffect', 'Contents')})
print('flags = 0x%X' % (h['MoreFlags'] | (h['CanPickup'] << 8) | (h['FlagsPortraitShadowType'] << 16)))
print('anim sets:', len(r['AnimSets']))
for si, a in enumerate(r['AnimSets']):
    print('AnimSet', si, 'Sfx', a['Sfx'], 'Flags', a['Flags'], 'Accel/AnimFlags', a['Acceleration'], 'Speed', a['Speed'], 'IsZForceApplied', a['IsZForceApplied'])
    for di, an in enumerate(a['PreloadedAnims']):
        frs = an['Frames']
        body = [f for f in frs if not f['IsTransitionFrame']]
        tail = frs[-1]
        dur = sum(f['Delay'] & 0x7F for f in body)
        print('   dir', di, 'frames', len(body), 'sum(Delay&0x7F) =', dur, ' last frame delay raw', body[-1]['Delay'], '(&0x7F=%d)' % (body[-1]['Delay'] & 0x7F), ' tail: IsTransition', tail['IsTransitionFrame'], 'Delay', tail['Delay'], 'TransformIndexLow', tail['TransformIndexLow'], 'CollisionOffset', tail['CollisionOffset'])
