import sys
sys.path.insert(0, r'C:/Users/casad/AppData/Local/Temp/claude/D--development-repo-alundra-casaengine-project-converter/b00d1a72-420d-4acc-ab34-0c25fbdad3a1/scratchpad/theme2-disc/critic')
import lib, bisect
from collections import defaultdict
hs = defaultdict(list)
for i in range(0, 0x4E):
    hs[lib.u32(0x80022D3C + 4 * i)].append(i)
starts = sorted(hs)
def handler_of(a):
    k = bisect.bisect_right(starts, a) - 1
    return starts[k] if k >= 0 else None
for name, t in (('PlayerTryAttack', 0x8002EAF4), ('PlayerTryAction', 0x8002EEAC), ('TryUseItem', 0x8002ED64)):
    print(name)
    for a in lib.callers(t):
        h = handler_of(a)
        print('  call at %x -> handler %x states %s' % (a, h, ' '.join('%02X' % s for s in hs.get(h, []))))
