import json, sys, io
sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding='utf-8')
d=json.load(open(r'D:/development/repo/alundra-casaengine-project-converter/data-extracted/data/ETC_RES.R.json',encoding='utf-8'))
keys=list(d.keys())
# ETC index i is keys[i+1] (key '3664' = " gagné!" = ETC 0x45)
def etc(i): return d[keys[i+1]]
print("ETC strings used by the chest handlers (real index = position-1, anchored on 0x45=' gagné!')")
for i in (0x45,0x46,0x47,0x48,0x49,0x4a,0x4b,0x4c,0x4d,0x4e,0x4f,0x50,0x51):
    print("0x%02x" % i, repr(etc(i)), 'offset', keys[i+1])
print('--- item names ETC 0x200+item')
for it in range(0,0x5c):
    print(it, hex(it), repr(etc(0x200+it)))
