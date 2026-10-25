import sys, json
sys.path.insert(0, r'C:/Users/casad/AppData/Local/Temp/claude/D--development-repo-alundra-casaengine-project-converter/b00d1a72-420d-4acc-ab34-0c25fbdad3a1/scratchpad/theme2-disc')
import bx
P = r'D:/development/repo/alundra-casaengine-project-converter/'
# item -> weapon record index table at 0x80028C14 (bytes) ; records of 13 bytes at 0x80028C78
print('item->record (0x80028C14), items 0..0x30:')
t = [bx.u8(0x80028C14 + i) for i in range(0, 0x30)]
print(t)
print('records at 0x80028C78 (13 bytes): [chargeable][ground a,type,delay,z][air a,type,delay,z][charge a,type,delay,z]')
for w in range(0, 13):
    b = [bx.u8(0x80028C78 + 13 * w + i) for i in range(13)]
    print(w, b)
print('anim flags b0,b1 at 0x80028D14 + 2*anim (anim 0..0x4F) -- those with b1 (spawn-capable):')
for a in range(0, 0x50):
    b0 = bx.u8(0x80028D14 + 2 * a); b1 = bx.u8(0x80028D14 + 2 * a + 1)
    if b0 or b1:
        print(hex(a), 'b0', b0, 'b1', b1)

bal = json.load(open(P + 'alundra-project/Data/balance.json', encoding='utf-8'))['EntriesBySpriteIndex']
h = bal[0]
print('hero entry 0: MaxHp', h['MaxHp'], 'AttackCount', h['AttackCount'], 'RecordSize', h['RecordSize'])
for i, a in enumerate(h['Attacks']):
    if a['AttackType'] or a['Power'] or a['AttackAttribute']:
        print('  hero attack row', i, '(anim', hex(i - 1) if i > 0 else '-', ')', a)
