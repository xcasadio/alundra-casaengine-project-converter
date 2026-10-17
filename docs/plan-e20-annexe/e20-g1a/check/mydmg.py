import os, sys, json
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), 'emu'))
import emu as E
m = E.make_machine()
R="D:/development/repo/alundra-casaengine-project-converter"
B=json.load(open(R+'/alundra-project/Data/balance.json',encoding='utf-8-sig'))['EntriesBySpriteIndex']
def sheet_bytes(b):
    s=[b['BalanceLevel'], b['RecordSize'], b['MaxHp']]+b['DamageResponses']+[b['AttackCount']]
    for a in b['Attacks'][:b['AttackCount']]:
        s += [a['AttackAttribute'], a['Power']]
    return bytes(s)
SHEET=0x80141000; ATT=0x80141800
sb=sheet_bytes(B[335])
for i,x in enumerate(sb): m.wb(SHEET+i,x)
# crate attack record: attr 1, power 128
for hp in (9, 5, 200):
    m.wb(ATT,1); m.wb(ATT+1,128)
    r = m.call(0x8004464C, ATT, SHEET, hp)
    print('crate(1,128) on Beannoide hp',hp,'->',r)
# the crate's own sheet attack list for sheet 291
bc=B[291]
print('crate sheet attack', bc['Attacks'][:1])
# a hero sword hit for comparison (attr1 power 1)
m.wb(ATT,1); m.wb(ATT+1,1); print('attr1 power1 hp9 ->', m.call(0x8004464C, ATT, SHEET, 9))
