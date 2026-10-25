import sys
sys.path.insert(0, r'C:/Users/casad/AppData/Local/Temp/claude/D--development-repo-alundra-casaengine-project-converter/b00d1a72-420d-4acc-ab34-0c25fbdad3a1/scratchpad/theme2-disc/critic')
import lib
from collections import defaultdict
for name, base in (('GiveItem 0x80033F00 table 0x800230D4', 0x800230D4), ('0x82 path 0x80034108 table 0x80023234', 0x80023234)):
    g = defaultdict(list)
    for i in range(0x57):
        g[lib.u32(base + 4*i)].append(i)
    print(name)
    for k, v in sorted(g.items()):
        print('  ', hex(k), ' '.join('%d' % x for x in v))
# who calls GiveItem and the 0x80034108
print('callers GiveItem', [hex(a) for a in lib.callers(0x80033F00)])
print('callers 0x80034108', [hex(a) for a in lib.callers(0x80034108)])
print('callers 0x80033ec8', [hex(a) for a in lib.callers(0x80033ec8)])
