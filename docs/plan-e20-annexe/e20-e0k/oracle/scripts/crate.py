import sys, os, json
DATA = r"D:/development/repo/alundra-casaengine-project-converter/data-extracted/data"
d = json.load(open(DATA + '/map_392.json', encoding='utf-8-sig'))
print(d.keys())
si = d['SpriteInfo']
print(si.keys())
recs = si['SpriteRecords']
print(len(recs))
ents = si['Entities']['Entities']
print(len(ents), ents[0] if ents else None)
seen = set()
for e in ents:
    if e is None: continue
    t = e['SpriteTableIndex']
    if t in seen: continue
    seen.add(t)
    r = recs[t]
    if r is None: continue
    H = r['Header']
    ub = H['Ubuff']
    flags = ub[0] | (ub[1] << 8) | (ub[2] << 16)
    print(t, 'flags=%06X' % flags, 'codes', ub[3:8], 'off', [x - 256 if x > 127 else x for x in ub[8:11]], 'size', ub[11:14], 'brk', ub[14], 'cont', ub[15], 'nanim', len(r['AnimSets']))
    for i, s in enumerate(r['AnimSets']):
        imp = (s['Unknown'] & 0xFF) | ((s['Flags'] & 0xFF) << 8)
        if imp & 0x8000: imp -= 0x10000
        print('   anim', i, 'speed', s['Speed'], 'impulse', imp, 'animflags', hex(s['Acceleration']), 'sfx', s['Sfx'])
