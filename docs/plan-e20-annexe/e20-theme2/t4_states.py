import sys
sys.path.insert(0, r'C:/Users/casad/AppData/Local/Temp/claude/D--development-repo-alundra-casaengine-project-converter/b00d1a72-420d-4acc-ab34-0c25fbdad3a1/scratchpad/theme2-disc')
import bx
# MovePlayer state switch jump table 0x80022D3C ; find extent: read until entries look like non-code addresses
from collections import defaultdict
g = defaultdict(list)
for i in range(0, 0x50):
    w = bx.u32(0x80022D3C + 4 * i)
    g[w].append(i)
for w, l in sorted(g.items()):
    print(hex(w), [hex(x) for x in l])
