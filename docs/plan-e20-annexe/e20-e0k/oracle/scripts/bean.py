import sys, os, json
DATA = r"D:/development/repo/alundra-casaengine-project-converter/data-extracted/data"
d = json.load(open(DATA + '/map_14.json', encoding='utf-8-sig'))
r = d['SpriteInfo']['SpriteRecords'][79]
ub = r['Header']['Ubuff']
flags = ub[0] | (ub[1] << 8) | (ub[2] << 16)
print('flags=%06X' % flags, 'codes', ub[3:8], 'off', [x - 256 if x > 127 else x for x in ub[8:11]], 'size', ub[11:14], 'brk', ub[14], 'cont', ub[15])
for i, s in enumerate(r['AnimSets']):
    imp = (s['Unknown'] & 0xFF) | ((s['Flags'] & 0xFF) << 8)
    if imp & 0x8000: imp -= 0x10000
    print('   anim', i, 'speed', s['Speed'], 'impulse', imp, 'animflags(+0xD)', hex(s['Acceleration']), 'sfx', s['Sfx'])
print(d['Info'])
