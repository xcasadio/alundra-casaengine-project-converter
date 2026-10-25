import sys
sys.path.insert(0, r'C:/Users/casad/AppData/Local/Temp/claude/D--development-repo-alundra-casaengine-project-converter/b00d1a72-420d-4acc-ab34-0c25fbdad3a1/scratchpad/theme2-disc/critic')
import lib
# find: addiu rt,$zero,V ... sw rt, 0x88(rs) within 6 instructions, in hero zone 0x8002E000..0x80038000
targets = [int(x, 16) for x in sys.argv[1:]]
for i in range(lib.N):
    a = lib.BASE + 4 * i
    if not (0x8002A000 <= a < 0x80039000): continue
    x = lib.W[i]
    if (x >> 26) == 9 and ((x >> 21) & 31) == 0:
        v = x & 0xFFFF
        if v in targets:
            rt = (x >> 16) & 31
            for j in range(1, 7):
                y = lib.W[i + j]
                if (y >> 26) == 0x2B and ((y >> 16) & 31) == rt and (y & 0xFFFF) == 0x88:
                    print('state %02X written at %x (func %x)' % (v, a + 4 * j, lib.func_of(a)))
                    break
                if (y >> 26) in (2, 3): break
