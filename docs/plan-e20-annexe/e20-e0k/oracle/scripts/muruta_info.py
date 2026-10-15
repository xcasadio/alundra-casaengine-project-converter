import sys, os, json
DATA = r"D:/development/repo/alundra-casaengine-project-converter/data-extracted/data"
d = json.load(open(DATA + '/map_115.json', encoding='utf-8-sig'))
si = d['SpriteInfo']
ents = si['Entities']['Entities']
for i, e in enumerate(ents):
    if e is None: continue
    if i in (6, 7, 8, 9, 12, 13, 14, 24, 25, 26, 27):
        print(i, {k: e[k] for k in ('SpriteTableIndex', 'XPos', 'YPos', 'Height', 'SpriteDirection', 'EventCodesA_LoadIndex', 'EventCodesC_TickIndex')})
r = si['SpriteRecords'][91]
ub = r['Header']['Ubuff']
print('flags=%06X' % (ub[0] | (ub[1] << 8) | (ub[2] << 16)), 'codes', ub[3:8], 'off', [x - 256 if x > 127 else x for x in ub[8:11]], 'size', ub[11:14])
for i, s in enumerate(r['AnimSets']):
    imp = (s['Unknown'] & 0xFF) | ((s['Flags'] & 0xFF) << 8)
    if imp & 0x8000: imp -= 0x10000
    print('  anim', i, 'speed', s['Speed'], 'impulse', imp, 'animflags', hex(s['Acceleration']))
print(d['Info']['Gravity'], d['Info']['ZViscosity'])
