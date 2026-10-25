import sys
sys.path.insert(0, r'C:/Users/casad/AppData/Local/Temp/claude/D--development-repo-alundra-casaengine-project-converter/b00d1a72-420d-4acc-ab34-0c25fbdad3a1/scratchpad/theme2-disc/critic')
import lib
print('item->record 0x80028C14:', [(i, lib.u8(0x80028C14+i)) for i in range(0, 0x31)])
for r in range(13):
    b = [lib.u8(0x80028C78 + 13*r + i) for i in range(13)]
    print(r, ['%02X'%x for x in b])
print('b0/b1 flags per anim 0..0x4F (0x80028D14):')
for a in range(0x50):
    b0 = lib.u8(0x80028D14+2*a); b1 = lib.u8(0x80028D14+2*a+1)
    if b0 or b1: print('  %02X b0=%d b1=%d' % (a,b0,b1))
