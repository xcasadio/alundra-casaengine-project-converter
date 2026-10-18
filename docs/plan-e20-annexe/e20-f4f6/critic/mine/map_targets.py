import json, sys
mp = int(sys.argv[1])
d = json.load(open("D:/development/repo/alundra-casaengine-project-converter/data-extracted/data/map_%d.json" % mp, encoding='utf-8'))
si = d['SpriteInfo']
recs = si['SpriteRecords']
ents = si['Entities']['Entities']
print('map', mp, 'records', len(ents))
for i, e in enumerate(ents):
    if e is None: continue
    loc = (e['SpriteDirection'] & 0x80) != 0
    idx = e['SpriteTableIndex']
    r = recs[idx] if loc else None
    if r is None:
        continue
    h = r['Header']
    fl = h['MoreFlags'] | (h['CanPickup'] << 8) | (h['FlagsPortraitShadowType'] << 16)
    if fl & 1:   # ClassA
        print(i, 'local', idx, 'flags %X' % fl, 'size', (h['SizeX'], h['SizeY'], h['SizeZ']), 'pos', (e['XPos'], e['YPos'], e['Height']), 'enabled', e['IsEnabled'], 'C/D/E', h['ProgramTick'], h['ProgramTouch'], h['ProgramDeactivate'])
