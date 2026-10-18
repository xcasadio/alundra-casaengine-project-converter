"""Which of the 395 exported sprite prefabs carry a CharacterController component (read-only).  python -B -I census_controller.py"""
import json, os
R = 'D:/development/repo/alundra-casaengine-project-converter/alundra-project/'
ai = json.load(open(R + 'AssetInfos.json', encoding='utf-8-sig'))
byid = {a['id']: a for a in ai['asset_infos']}
d = json.load(open(R + 'Data/sprite-records.json'))
none = []
checked = 0
for k, v in d.items():
    a = byid.get(k)
    if not a:
        continue
    p = R + a['file_name'].replace(chr(92), '/')
    if not os.path.exists(p):
        continue
    checked += 1
    txt = open(p, encoding='utf-8-sig').read()
    if 'CharacterController' not in txt:
        none.append(v['BankKey'])
print('prefabs checked', checked, 'without CharacterController:', len(none), none)
want = {'alundra_3', 'alundra_4', 'alundra_5', 'alundra_6', 'alundra_7', 'alundra_8', 'alundra_9', 'alundra_10', 'alundra_11', 'alundra_12', 'alundra_13', 'alundra_14', 'alundra_15', 'alundra_27', 'alundra_28', 'alundra_29'}
print('hero projectile prefabs without controller:', sorted(want & set(none)))
