import sys
sys.path.insert(0, r'C:/Users/casad/AppData/Local/Temp/claude/D--development-repo-alundra-casaengine-project-converter/b00d1a72-420d-4acc-ab34-0c25fbdad3a1/scratchpad/t2-chest')
import bx
print("ApplyItem jump table 0x8002308C (item-0x45, 18 entries)")
for i in range(18):
    print("  item 0x%02x (%d) -> %08x" % (0x45+i, 0x45+i, bx.u32(0x8002308C+4*i)))
print("GiveItem jump table 0x800230D4 (0x57 entries)")
prev=None
for i in range(0x57):
    t=bx.u32(0x800230D4+4*i)
    print("  item 0x%02x (%d) -> %08x" % (i,i,t))
