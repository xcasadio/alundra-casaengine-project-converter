import json, sys
R='D:/development/repo/alundra-casaengine-project-converter/'
d=json.load(open(R+'alundra-project/Data/sprite-records.json'))
first=next(iter(d.values()))
print(sorted(first.keys()))
rows=[]
for k,v in d.items():
    bk=v.get('BankKey') or v.get('bankKey')
    rows.append((k,bk,v))
want=[f'alundra_{i}' for i in list(range(3,16))+[27,28,29]]
for k,bk,v in rows:
    if bk in want:
        print(bk, {kk:v[kk] for kk in v if kk in ('Name','Flags','CodeA','CodeB','CodeC','CodeD','CodeE','CodeF','BreakEffect','Contents','ProgramLoad','ProgramTick','ProgramTouch','ProgramDeactivate','ProgramInteract','ProgramF','ProgramB','HeaderFlags')})
