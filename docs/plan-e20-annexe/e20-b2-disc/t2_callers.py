import sys
sys.path.insert(0, r'C:/Users/casad/AppData/Local/Temp/claude/D--development-repo-alundra-casaengine-project-converter/b00d1a72-420d-4acc-ab34-0c25fbdad3a1/scratchpad/t2-proj/o8')
import bx
def callers(target):
    w = 0x0C000000 | ((target >> 2) & 0x03FFFFFF)
    out = []
    a = 0x80020000
    end = 0x80020000 + len(bx.data) - 0x800
    while a < end:
        if bx.u32(a) == w:
            out.append(a)
        a += 4
    return out
for t in [int(x, 16) for x in sys.argv[1:]]:
    print(hex(t), [hex(c) for c in callers(t)])
