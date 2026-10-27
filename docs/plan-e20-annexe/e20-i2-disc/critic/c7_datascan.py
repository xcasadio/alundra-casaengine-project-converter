import sys
sys.path.insert(0, r'C:/Users/casad/AppData/Local/Temp/claude/D--development-repo-alundra-casaengine-project-converter/b00d1a72-420d-4acc-ab34-0c25fbdad3a1/scratchpad/t2-next/i2-critic')
import bx, struct
END = 0x800CA800
want = int(sys.argv[1], 0)
# 32-bit aligned words
for a in range(0x80020000, END, 4):
    if bx.u32(a) == want:
        ctx = [bx.u32(a + 4*i) for i in range(-3, 4)]
        print('W32 %08x : %s' % (a, ' '.join('%08x' % c for c in ctx)))
# 16-bit aligned halfwords
for a in range(0x80020000, END, 2):
    if bx.u16(a) == want and (a % 4):
        ctx = [bx.u16(a + 2*i) for i in range(-3, 4)]
        print('H16 %08x : %s' % (a, ' '.join('%04x' % c for c in ctx)))
