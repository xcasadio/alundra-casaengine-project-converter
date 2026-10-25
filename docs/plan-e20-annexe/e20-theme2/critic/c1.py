import sys
sys.path.insert(0, r'C:/Users/casad/AppData/Local/Temp/claude/D--development-repo-alundra-casaengine-project-converter/b00d1a72-420d-4acc-ab34-0c25fbdad3a1/scratchpad/theme2-disc/critic')
import lib
from collections import defaultdict
# state jump table 0x80022D3C
g = defaultdict(list)
for i in range(0, 0x4E):
    g[lib.u32(0x80022D3C + 4 * i)].append(i)
print('MovePlayer state table 0x80022D3C: handler -> states')
for k, v in sorted(g.items()):
    print(hex(k), ' '.join('%02X' % x for x in v))
print()
for name, base in (('start', 0x8002339C), ('cancel', 0x800233EC), ('driver', 0x800235F4)):
    print(name, base)
    row = []
    for i in range(0, 0x22):
        row.append('%02X:%08x' % (0x1F + i, lib.u32(base + 4 * i)))
    print(' '.join(row))
