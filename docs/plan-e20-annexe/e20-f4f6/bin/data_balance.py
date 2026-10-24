import json,sys
d=json.load(open('D:/development/repo/alundra-casaengine-project-converter/alundra-project/Data/balance.json', encoding='utf-8-sig'))['EntriesBySpriteIndex']
print(len(d))
def show(i):
    r=d[i]
    lvl=0
    print("== entry %d"%i)
    while r is not None and lvl<12:
        atk=r.get('Attacks') or []
        nz=[(k,a['AttackType'],a['UsesEquipmentPower'],a['AttackAttribute'],a['Power']) for k,a in enumerate(atk) if (a['AttackType'] or a['AttackAttribute'] or a['Power'])]
        print("  level=%s recsize=%s maxhp=%s resp=%s attackcount=%s nonzero attacks (idx,type,equip,attr,power)=%s"%(r['BalanceLevel'],r['RecordSize'],r['MaxHp'],r['DamageResponses'],r['AttackCount'],nz))
        r=r.get('NextLevel'); lvl+=1
for a in sys.argv[1:]: show(int(a))
