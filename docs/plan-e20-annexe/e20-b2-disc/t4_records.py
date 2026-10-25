import sys, json, csv
sys.path.insert(0, r'C:/Users/casad/AppData/Local/Temp/claude/D--development-repo-alundra-casaengine-project-converter/b00d1a72-420d-4acc-ab34-0c25fbdad3a1/scratchpad/t2-proj/o8')
import bx
P = r'D:/development/repo/alundra-casaengine-project-converter/'
names = []
with open(P + 'alundra-datas-analyser/AlundraTools/AlundraTools/EntityNames.csv', encoding='utf-8-sig') as f:
    rd = csv.reader(f, delimiter=';')
    next(rd)
    for row in rd:
        names.append(row)
print('item -> record (0x80028C14), items 0..0x20:')
for i in range(0, 0x21):
    print(' item %2d (%s) -> record %d' % (i, names[30 + i][1] if 30 + i < len(names) else '?', bx.u8(0x80028C14 + i)))
print('records at 0x80028C78 (13 bytes): [chargeable][ground anim,type,delay,z][air anim,type,delay,z][charge anim,type,delay,z]')
recs = []
for w in range(0, 13):
    b = [bx.u8(0x80028C78 + 13 * w + i) for i in range(13)]
    recs.append(b)
    print(w, ['%02x' % x for x in b], b)
print('record 0 == record 11 ?', recs[0] == recs[11])
print('which items point to which record:')
by = {}
for i in range(0, 0x30):
    by.setdefault(bx.u8(0x80028C14 + i), []).append(i)
for k in sorted(by):
    print(' record', k, 'items', by[k])
