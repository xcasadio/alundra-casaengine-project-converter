import sys
sys.dont_write_bytecode = True
TOOLS = r"D:/development/repo/alundra-casaengine-project-converter/docs/plan-e20-annexe/e20-f3/oracle/tools"
sys.path.insert(0, TOOLS)
sys.path.append(r'C:/Users/casad/AppData/Roaming/Python/Python314/site-packages')
import pworld as P
m, meta = P.setup(item=None, hero_px=(100,100), direction=24, targets=[(12,12,0)], seed=0x2468ACE0)
hs = sorted(m.hooks)
print(len(hs))
for a in hs:
    if 0x8003B000 <= a < 0x8003C800 or 0x80038E00 <= a < 0x80039400:
        print(hex(a), m.hooks[a].__name__ if hasattr(m.hooks[a], '__name__') else m.hooks[a])
print([hex(a) for a in hs if a < 0x80038000 or a>0x8003D000][:80])
