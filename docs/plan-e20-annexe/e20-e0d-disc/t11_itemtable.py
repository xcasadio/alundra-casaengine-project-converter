import sys, io, json
sys.path.insert(0, 'C:/Users/casad/AppData/Local/Temp/claude/D--development-repo-alundra-casaengine-project-converter/b00d1a72-420d-4acc-ab34-0c25fbdad3a1/scratchpad/t2-chest')
import bx
sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding='utf-8')
etc = json.load(open('D:/development/repo/alundra-casaengine-project-converter/data-extracted/data/ETC_RES.R.json', encoding='utf-8'))
ek = list(etc.keys())
def nm(i):
    k = 0x200 + i + 1
    return etc[ek[k]] if k < len(ek) else None
# contents values that occur in chests (from t7_census.out): item -> count outside map 0
contents = {1:1,2:2,3:1,4:2,6:1,14:1,16:1,19:1,26:1,27:1,28:1,32:1,36:108,37:14,38:9,39:17,41:26,43:1,44:2,45:1,46:1,47:2,48:1,49:1,50:1,54:1,57:1,60:1,61:47,62:1,63:2,64:2,65:1,66:1,67:1,69:2,71:6,72:39,73:1,79:54,80:4,83:38}
print('item  name                 byte4(600-tick rule) byte5(sound; 0 = key object: fanfare) byte6 byte7(max stock)  chests(outside map 0)')
for i in sorted(contents):
    b = 0x800C5F7C + 8 * i
    print('%3d 0x%02x %-20s %d  %2d  %3d  %d   x%d' % (i, i, nm(i), bx.u8(b+4), bx.u8(b+5), bx.u8(b+6), bx.u8(b+7), contents[i]))
print()
print('key objects (byte5 == 0) among ALL table rows 1..0x61:', [i for i in range(1, 0x62) if bx.u8(0x800C5F7C + 8*i + 5) == 0])
print('rows with sound 52:', [i for i in range(1, 0x62) if bx.u8(0x800C5F7C + 8*i + 5) == 52])
print('rows with sound 53:', [i for i in range(1, 0x62) if bx.u8(0x800C5F7C + 8*i + 5) == 53])
print('rows with sound 54:', [i for i in range(1, 0x62) if bx.u8(0x800C5F7C + 8*i + 5) == 54])
