import sys
sys.path.insert(0, r'C:/Users/casad/AppData/Local/Temp/claude/D--development-repo-alundra-casaengine-project-converter/b00d1a72-420d-4acc-ab34-0c25fbdad3a1/scratchpad/theme2-disc')
import bx

print('start switch table 0x8002339C (s0-0x1F):')
for i, w in enumerate(bx.words(0x8002339C, 20)):
    print(hex(0x1F + i), hex(w))
print('driver table 0x800235F4:')
for i, w in enumerate(bx.words(0x800235F4, 20)):
    print(hex(0x1F + i), hex(w))
print('cancel table 0x800233EC:')
for i, w in enumerate(bx.words(0x800233EC, 20)):
    print(hex(0x1F + i), hex(w))
