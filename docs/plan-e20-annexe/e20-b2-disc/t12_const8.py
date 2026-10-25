import sys
sys.path.insert(0, r'C:/Users/casad/AppData/Local/Temp/claude/D--development-repo-alundra-casaengine-project-converter/b00d1a72-420d-4acc-ab34-0c25fbdad3a1/scratchpad/t2-proj/o8')
import scan_local as S
names = {0x8004e428: 'GetNumberOfItem', 0x8004e530: 'AddOneItemIfUnlocked/AddItem', 0x80033ec8: 'AddItem(wrapper)', 0x80034108: 'HandleMapTriggerCommand', 0x8004e5c4: 'UseItem',
         0x8004e484: 'SetPlayerWeaponId', 0x8004e18c: 'GetItemIdFromSlotId', 0x80033f00: 'GiveItem'}
for t, n in names.items():
    for c in S.callsites(t):
        a = S.const_args(c)
        flag = '  <== arg 8' if a.get('a0') == 8 else ''
        if a.get('a0') == 8 or n in ('SetPlayerWeaponId',):
            print(hex(c), n, a, 'in func', hex(S.func_of(c)), flag)
print('--- compares of an item id with 8 (slti/sltiu/xori/addiu -8 right before a branch) are not searched; only constant-arg calls above.')
