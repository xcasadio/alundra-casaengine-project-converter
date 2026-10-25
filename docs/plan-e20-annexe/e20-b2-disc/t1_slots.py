import sys, json
sys.path.insert(0, r'C:/Users/casad/AppData/Local/Temp/claude/D--development-repo-alundra-casaengine-project-converter/b00d1a72-420d-4acc-ab34-0c25fbdad3a1/scratchpad/t2-proj/o8')
import bx
P = r'D:/development/repo/alundra-casaengine-project-converter/'
# the property table read by 0x8004E18C: entries of 10 bytes at 0x800B9FE8: slot(s16) flag(u16) rank(s16) max(s16) icon(s16)
print('table at 0x800B9FE8 (binary), items 0..20:')
for i in range(0, 21):
    a = 0x800B9FE8 + 10 * i
    print(i, 'slot', bx.s16(a), 'flag', bx.u16(a + 2), 'rank', bx.s16(a + 4), 'max', bx.s16(a + 6), 'icon', bx.s16(a + 8))
ip = json.load(open(P + 'alundra-project/Data/items-properties.json', encoding='utf-8'))
print('items-properties.json (export) items 0..20:')
for i in range(0, 21):
    print(i, ip[i])
# exported == binary ?
diffs = []
for i in range(0, 0x80):
    a = 0x800B9FE8 + 10 * i
    b = [bx.s16(a), bx.u16(a + 2), bx.s16(a + 4), bx.s16(a + 6), bx.s16(a + 8)]
    if i < len(ip):
        e = ip[i]
        vals = list(e.values()) if isinstance(e, dict) else list(e)
        if [int(v) for v in vals[:5]] != b:
            diffs.append((i, b, e))
print('binary vs export diffs (first 5):', diffs[:5], 'count', len(diffs))
